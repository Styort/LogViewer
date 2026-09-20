using System;
using System.Collections.Generic;
using System.Text;

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
    /// <c>&lt;prefix:event …&gt;…&lt;/prefix:event&gt;</c> documents. The unfinished tail stays
    /// in the buffer until the next chunk. The prefix is taken from the start tag (usually
    /// <c>log4j</c>) so a custom namespace prefix still frames.
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

        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly int _maxBufferChars;
        private bool _overflowed;

        public Log4jXmlFrameSplitter(int maxBufferChars = DefaultMaxBufferChars)
        {
            if (maxBufferChars < 64)
                throw new ArgumentOutOfRangeException(nameof(maxBufferChars));
            _maxBufferChars = maxBufferChars;
        }

        /// <summary>Unfinished text still waiting for a closing tag (or a complete start tag).</summary>
        public string RemainingText => _buffer.ToString();

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
                _buffer.Append(chunk);

            if (_buffer.Length > _maxBufferChars)
            {
                _buffer.Clear();
                _overflowed = true;
                return new string[0];
            }

            var frames = new List<string>();
            while (TryExtractFrame(frames))
            {
            }

            return frames;
        }

        private bool TryExtractFrame(List<string> frames)
        {
            string s = _buffer.ToString();
            int start;
            string prefix;
            if (!TryFindStartTag(s, out start, out prefix))
            {
                CompactWhenNoStart(s);
                return false;
            }

            if (start > 0)
            {
                _buffer.Remove(0, start);
                s = _buffer.ToString();
                start = 0;
            }

            string endTag = "</" + prefix + ":event>";
            int end = s.IndexOf(endTag, StringComparison.Ordinal);
            if (end < 0)
                return false;

            int frameEnd = end + endTag.Length;
            frames.Add(s.Substring(0, frameEnd));
            _buffer.Remove(0, frameEnd);
            return true;
        }

        /// <summary>
        /// Finds <c>&lt;prefix:event</c> followed by whitespace, <c>&gt;</c>, or <c>/</c>.
        /// Incomplete tags at the end of the buffer wait for more data.
        /// </summary>
        private static bool TryFindStartTag(string s, out int start, out string prefix)
        {
            start = -1;
            prefix = null;
            int i = 0;
            while (i < s.Length)
            {
                int lt = s.IndexOf('<', i);
                if (lt < 0)
                    return false;

                int nameStart = lt + 1;
                if (nameStart >= s.Length)
                    return false;

                if (!IsNameStart(s[nameStart]))
                {
                    i = lt + 1;
                    continue;
                }

                int colon = s.IndexOf(':', nameStart);
                if (colon < 0)
                    return false;

                if (colon == nameStart)
                {
                    i = lt + 1;
                    continue;
                }

                bool nameOk = true;
                for (int n = nameStart; n < colon; n++)
                {
                    if (!IsNameChar(s[n]))
                    {
                        nameOk = false;
                        break;
                    }
                }

                if (!nameOk)
                {
                    i = lt + 1;
                    continue;
                }

                const string Event = "event";
                if (colon + 1 + Event.Length > s.Length)
                    return false;

                if (string.Compare(s, colon + 1, Event, 0, Event.Length, StringComparison.Ordinal) != 0)
                {
                    i = lt + 1;
                    continue;
                }

                int after = colon + 1 + Event.Length;
                if (after >= s.Length)
                    return false;

                char c = s[after];
                if (c != '>' && c != '/' && !char.IsWhiteSpace(c))
                {
                    i = lt + 1;
                    continue;
                }

                start = lt;
                prefix = s.Substring(nameStart, colon - nameStart);
                return true;
            }

            return false;
        }

        private void CompactWhenNoStart(string s)
        {
            int lastLt = s.LastIndexOf('<');
            if (lastLt < 0)
            {
                _buffer.Clear();
                return;
            }

            string tail = s.Substring(lastLt);
            if (tail.StartsWith("</", StringComparison.Ordinal))
            {
                _buffer.Clear();
                return;
            }

            if (lastLt > 0)
                _buffer.Remove(0, lastLt);
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
