using System;
using System.Collections.Generic;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Splits a TCP byte/char stream into complete log4j XML events.
    /// </summary>
    /// <remarks>
    /// UDP is datagram-oriented: NLogViewer / Chainsaw send one XML event per packet, so
    /// <c>UdpClient.Receive</c> already yields a full frame. TCP is a byte stream: events are
    /// concatenated and a segment can end in the middle of a tag or a UTF-8 character.
    /// This class accumulates text and yields only complete
    /// <c>&lt;prefix:event …&gt;…&lt;/prefix:event&gt;</c> documents (or a self-closing
    /// <c>&lt;prefix:event … /&gt;</c>). The unfinished tail stays in the buffer until the next chunk.
    /// The prefix is taken from the start tag (usually <c>log4j</c>) so a custom namespace prefix still frames.
    ///
    /// The end tag is searched outside CDATA sections and comments: a message that quotes
    /// <c>&lt;/log4j:event&gt;</c> inside CDATA must not cut the event. Scanning resumes where the previous
    /// chunk stopped, so a large event arriving in many small segments is scanned once, not once per segment.
    ///
    /// <see cref="DefaultMaxBufferChars"/> caps growth if the peer sends non-XML garbage
    /// (port scan, HTTP, never-closed event). On overflow the buffer is dropped; the caller
    /// can surface a synthetic error and keep the listener alive.
    /// </remarks>
    public sealed class Log4jXmlFrameSplitter
    {
        /// <summary>
        /// 1 MiB of decoded text. Large enough for a fat throwable; larger usually means the
        /// stream is not log4j XML.
        /// </summary>
        public const int DefaultMaxBufferChars = 1024 * 1024;

        private const int InitialBufferChars = 4096;

        private const string CDataStart = "<![CDATA[";
        private const string CDataEnd = "]]>";
        private const string CommentStart = "<!--";
        private const string CommentEnd = "-->";

        private readonly int _maxBufferChars;
        private bool _overflowed;

        // Pending text is _buffer[_head.._tail). Consumed frames only move _head; the array is compacted
        // lazily, so appending a segment never copies the whole pending event.
        private char[] _buffer = new char[InitialBufferChars];
        private int _head;
        private int _tail;

        // State of the event whose start tag has been read; positions are absolute indexes into _buffer.
        private bool _inEvent;
        private string _endTag;
        private int _scan;
        private string _awaiting;

        public Log4jXmlFrameSplitter(int maxBufferChars = DefaultMaxBufferChars)
        {
            if (maxBufferChars < 64)
                throw new ArgumentOutOfRangeException(nameof(maxBufferChars));
            _maxBufferChars = maxBufferChars;
        }

        /// <summary>Unfinished text still waiting for a closing tag (or a complete start tag).</summary>
        public string RemainingText => new string(_buffer, _head, _tail - _head);

        /// <summary>
        /// True after the last <see cref="Append"/> that exceeded <see cref="DefaultMaxBufferChars"/>.
        /// Cleared by the next successful consume via <see cref="ConsumeOverflow"/>.
        /// </summary>
        public bool ConsumeOverflow()
        {
            if (!_overflowed)
                return false;
            _overflowed = false;
            return true;
        }

        /// <summary>
        /// Append a decoded TCP chunk and return every complete XML event found.
        /// Empty or null chunks are ignored. Does not throw on garbage.
        /// </summary>
        public IReadOnlyList<string> Append(string chunk)
        {
            if (!string.IsNullOrEmpty(chunk))
                Write(chunk);

            if (_tail - _head > _maxBufferChars)
            {
                Reset();
                _overflowed = true;
                return new string[0];
            }

            var frames = new List<string>();
            while (TryExtractFrame(frames))
            {
            }

            return frames;
        }

        private void Write(string chunk)
        {
            int pending = _tail - _head;
            if (_buffer.Length - _tail < chunk.Length)
            {
                int required = pending + chunk.Length;
                // Compact into the same array when consumed text frees enough room, otherwise grow.
                char[] target = _buffer.Length >= required * 2 ? _buffer : new char[Math.Max(required * 2, _buffer.Length * 2)];
                Array.Copy(_buffer, _head, target, 0, pending);
                Shift(-_head);
                _buffer = target;
            }

            chunk.CopyTo(0, _buffer, _tail, chunk.Length);
            _tail += chunk.Length;
        }

        private void Shift(int delta)
        {
            _head += delta;
            _tail += delta;
            _scan += delta;
        }

        private void Reset()
        {
            // One fat event must not pin a megabyte-sized array for the life of the connection.
            if (_buffer.Length > InitialBufferChars * 16)
                _buffer = new char[InitialBufferChars];
            _head = _tail = 0;
            _inEvent = false;
            _endTag = null;
            _awaiting = null;
            _scan = 0;
        }

        private bool TryExtractFrame(List<string> frames)
        {
            if (!_inEvent)
            {
                if (!TryEnterEvent(frames, out bool emitted))
                    return false;
                if (emitted)
                    return true;
            }

            while (true)
            {
                if (_awaiting != null)
                {
                    int close = IndexOf(_awaiting, _scan);
                    if (close < 0)
                    {
                        // Keep the last few chars: the terminator may be split across segments.
                        _scan = Math.Max(_scan, _tail - (_awaiting.Length - 1));
                        return false;
                    }
                    _scan = close + _awaiting.Length;
                    _awaiting = null;
                    continue;
                }

                int lt = IndexOf('<', _scan);
                if (lt < 0)
                {
                    _scan = _tail;
                    return false;
                }

                var cdata = MatchAt(CDataStart, lt);
                var comment = MatchAt(CommentStart, lt);
                var end = MatchAt(_endTag, lt);
                if (cdata == Match.NeedMore || comment == Match.NeedMore || end == Match.NeedMore)
                {
                    _scan = lt;
                    return false;
                }

                if (cdata == Match.Yes)
                {
                    _awaiting = CDataEnd;
                    _scan = lt + CDataStart.Length;
                    continue;
                }

                if (comment == Match.Yes)
                {
                    _awaiting = CommentEnd;
                    _scan = lt + CommentStart.Length;
                    continue;
                }

                if (end == Match.Yes)
                {
                    int p = lt + _endTag.Length;
                    while (p < _tail && char.IsWhiteSpace(_buffer[p]))
                        p++;
                    if (p >= _tail)
                    {
                        _scan = lt;
                        return false;
                    }

                    if (_buffer[p] == '>')
                    {
                        Emit(frames, p + 1);
                        _inEvent = false;
                        return true;
                    }
                }

                _scan = lt + 1;
            }
        }

        /// <summary>
        /// Moves <see cref="_head"/> to the next start tag and reads it. A self-closing event is emitted
        /// right away (<paramref name="emitted"/>); otherwise the event body is scanned by the caller.
        /// </summary>
        private bool TryEnterEvent(List<string> frames, out bool emitted)
        {
            emitted = false;
            if (!TryFindStartTag(out int start, out string prefix, out int nameEnd))
            {
                CompactWhenNoStart();
                return false;
            }

            _head = start;
            int gt = FindStartTagEnd(nameEnd);
            if (gt < 0)
                return false;

            if (_buffer[gt - 1] == '/')
            {
                // <log4j:event ... />: searching for </log4j:event> here used to swallow the next event.
                Emit(frames, gt + 1);
                emitted = true;
                return true;
            }

            _inEvent = true;
            _endTag = "</" + prefix + ":event";
            _scan = gt + 1;
            _awaiting = null;
            return true;
        }

        private void Emit(List<string> frames, int frameEnd)
        {
            frames.Add(new string(_buffer, _head, frameEnd - _head));
            _head = frameEnd;
            if (_head == _tail)
                Reset();
        }

        /// <summary>
        /// Finds <c>&lt;prefix:event</c> followed by whitespace, <c>&gt;</c>, or <c>/</c>.
        /// Incomplete tags at the end of the buffer wait for more data.
        /// </summary>
        private bool TryFindStartTag(out int start, out string prefix, out int nameEnd)
        {
            start = -1;
            prefix = null;
            nameEnd = -1;
            int i = _head;
            while (i < _tail)
            {
                int lt = IndexOf('<', i);
                if (lt < 0)
                    return false;

                int nameStart = lt + 1;
                if (nameStart >= _tail)
                    return false;

                if (!IsNameStart(_buffer[nameStart]))
                {
                    i = lt + 1;
                    continue;
                }

                int colon = IndexOf(':', nameStart);
                if (colon < 0)
                    return false;

                bool nameOk = colon > nameStart;
                for (int n = nameStart; nameOk && n < colon; n++)
                    nameOk = IsNameChar(_buffer[n]);

                if (!nameOk)
                {
                    i = lt + 1;
                    continue;
                }

                const string Event = "event";
                var match = MatchAt(Event, colon + 1);
                if (match == Match.NeedMore)
                    return false;
                if (match == Match.No)
                {
                    i = lt + 1;
                    continue;
                }

                int after = colon + 1 + Event.Length;
                if (after >= _tail)
                    return false;

                char c = _buffer[after];
                if (c != '>' && c != '/' && !char.IsWhiteSpace(c))
                {
                    i = lt + 1;
                    continue;
                }

                start = lt;
                prefix = new string(_buffer, nameStart, colon - nameStart);
                nameEnd = after;
                return true;
            }

            return false;
        }

        /// <summary>Index of the <c>&gt;</c> closing the start tag; attribute values may contain <c>&gt;</c>.</summary>
        private int FindStartTagEnd(int from)
        {
            char quote = '\0';
            for (int i = from; i < _tail; i++)
            {
                char c = _buffer[i];
                if (quote != '\0')
                {
                    if (c == quote)
                        quote = '\0';
                }
                else if (c == '"' || c == '\'')
                {
                    quote = c;
                }
                else if (c == '>')
                {
                    return i;
                }
            }
            return -1;
        }

        private void CompactWhenNoStart()
        {
            int lastLt = -1;
            for (int i = _tail - 1; i >= _head; i--)
            {
                if (_buffer[i] == '<')
                {
                    lastLt = i;
                    break;
                }
            }

            if (lastLt < 0 || (lastLt + 1 < _tail && _buffer[lastLt + 1] == '/'))
            {
                Reset();
                return;
            }

            _head = lastLt;
        }

        private enum Match
        {
            No,
            Yes,
            NeedMore
        }

        private Match MatchAt(string pattern, int position)
        {
            for (int k = 0; k < pattern.Length; k++)
            {
                int p = position + k;
                if (p >= _tail)
                    return Match.NeedMore;
                if (_buffer[p] != pattern[k])
                    return Match.No;
            }
            return Match.Yes;
        }

        private int IndexOf(char c, int from)
        {
            int index = Array.IndexOf(_buffer, c, from, _tail - from);
            return index;
        }

        private int IndexOf(string pattern, int from)
        {
            for (int i = from; i <= _tail - pattern.Length; i++)
            {
                i = IndexOf(pattern[0], i);
                if (i < 0 || i > _tail - pattern.Length)
                    return -1;
                if (MatchAt(pattern, i) == Match.Yes)
                    return i;
            }
            return -1;
        }

        private static bool IsNameStart(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
        }

        private static bool IsNameChar(char c)
        {
            return IsNameStart(c) || (c >= '0' && c <= '9') || c == '.' || c == '-';
        }
    }
}
