using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Reads a template-formatted log stream: header line is parsed, continuation lines go into Message.
    /// </summary>
    internal static class TemplateFileLogReader
    {
        public const int BufferSize = 256 * 1024;
        private const int SeekBlockSize = 1024 * 1024;

        internal struct RangeSeekResult
        {
            public bool FilterByMinTime;
            public DateTime MinTime;
        }

        private struct HeaderHit
        {
            public long LineStart;
            public DateTime Time;
        }

        public static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize);
        }

        public static RangeSeekResult SeekToRange(
            Stream stream,
            ImportRange range,
            Encoding encoding,
            TemplateLogParser parser,
            TemplateParseLayout layout,
            CancellationToken cancellationToken)
        {
            var result = new RangeSeekResult();
            if (stream == null || !stream.CanSeek)
                return result;

            if (range == null || range.Mode == ImportRangeMode.EntireFile)
            {
                stream.Position = 0;
                return result;
            }

            if (range.Mode == ImportRangeMode.LastBytes)
            {
                SeekToByteTail(stream, range.LastBytes > 0 ? range.LastBytes : ImportRange.DefaultLastBytes);
                return result;
            }

            SeekToTimeTail(stream, encoding, parser, layout,
                range.LastDuration > TimeSpan.Zero ? range.LastDuration : TimeSpan.FromHours(2),
                cancellationToken, out result);
            return result;
        }

        public static void SeekToByteTail(Stream stream, long maxBytes)
        {
            if (stream == null || !stream.CanSeek)
                return;

            long length = stream.Length;
            if (maxBytes <= 0 || length <= maxBytes)
            {
                stream.Position = 0;
                return;
            }

            stream.Position = length - maxBytes;
            SkipPartialLine(stream);
        }

        public static void Read(
            Stream stream,
            Encoding encoding,
            TemplateLogParser parser,
            TemplateParseLayout layout,
            string address,
            Action<LogEntry> onEntry,
            CancellationToken cancellationToken,
            Action<long, long> onProgress)
        {
            if (stream == null || parser == null || layout == null || !layout.IsValid || onEntry == null)
                return;

            bool detectBom = !stream.CanSeek || stream.Position == 0;
            long origin = stream.CanSeek ? stream.Position : 0;
            long length = stream.CanSeek ? stream.Length : 0;
            long span = length - origin;
            if (span <= 0)
                span = 1;

            using (var reader = new StreamReader(stream, encoding, detectBom, BufferSize, leaveOpen: true))
            {
                LogEntry current = null;
                var extra = new StringBuilder();
                long lastReportedPos = origin;
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (StringUtils.ContainsAnyOf(line, layout.LogTypeMarkers))
                    {
                        Flush(ref current, extra, onEntry);
                        current = parser.ParseHeaderLine(line, layout, address);
                    }
                    else if (current != null)
                    {
                        extra.Append(Environment.NewLine).Append(line);
                    }

                    if (onProgress != null && stream.CanSeek && stream.Position - lastReportedPos > 65536)
                    {
                        lastReportedPos = stream.Position;
                        onProgress(stream.Position - origin, span);
                    }
                }

                Flush(ref current, extra, onEntry);
            }
        }

        private static void SeekToTimeTail(
            Stream stream,
            Encoding encoding,
            TemplateLogParser parser,
            TemplateParseLayout layout,
            TimeSpan duration,
            CancellationToken cancellationToken,
            out RangeSeekResult result)
        {
            result = new RangeSeekResult();
            if (encoding == null || parser == null || layout == null || !layout.IsValid || layout.DateIndex < 0)
            {
                SeekToByteTail(stream, ImportRange.DefaultLastBytes);
                return;
            }

            long length = stream.Length;
            if (length == 0)
            {
                stream.Position = 0;
                return;
            }

            if (!TryFindMaxTimeNearEnd(stream, encoding, parser, layout, cancellationToken, out DateTime maxTime))
            {
                SeekToByteTail(stream, ImportRange.DefaultLastBytes);
                return;
            }

            DateTime cutoff = maxTime - duration;
            result.MinTime = cutoff;

            long cursor = length;
            long candidate = -1;
            bool foundBelowCutoff = false;
            bool nonMonotonic = false;

            while (cursor > 0 && !cancellationToken.IsCancellationRequested)
            {
                long rawStart = Math.Max(0, cursor - SeekBlockSize);
                var hits = ScanHeaders(stream, encoding, parser, layout, rawStart, cursor, cancellationToken, out long alignedFrom);

                for (int i = hits.Count - 1; i >= 0; i--)
                {
                    if (hits[i].Time >= cutoff)
                    {
                        if (foundBelowCutoff)
                        {
                            nonMonotonic = true;
                            break;
                        }

                        candidate = hits[i].LineStart;
                    }
                    else
                    {
                        foundBelowCutoff = true;
                    }
                }

                if (nonMonotonic || foundBelowCutoff)
                    break;

                if (alignedFrom <= 0 || alignedFrom >= cursor)
                    break;

                cursor = alignedFrom;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (nonMonotonic)
            {
                SeekToByteTail(stream, ImportRange.DefaultLastBytes);
                result.FilterByMinTime = true;
                return;
            }

            stream.Position = candidate >= 0 ? candidate : 0;
        }

        private static bool TryFindMaxTimeNearEnd(
            Stream stream,
            Encoding encoding,
            TemplateLogParser parser,
            TemplateParseLayout layout,
            CancellationToken cancellationToken,
            out DateTime maxTime)
        {
            maxTime = default(DateTime);
            long length = stream.Length;
            long start = Math.Max(0, length - SeekBlockSize);
            var hits = ScanHeaders(stream, encoding, parser, layout, start, length, cancellationToken, out _);
            if (hits.Count == 0 && start > 0)
            {
                start = Math.Max(0, length - 4L * SeekBlockSize);
                hits = ScanHeaders(stream, encoding, parser, layout, start, length, cancellationToken, out _);
            }

            if (hits.Count == 0)
                return false;

            maxTime = hits[0].Time;
            for (int i = 1; i < hits.Count; i++)
            {
                if (hits[i].Time > maxTime)
                    maxTime = hits[i].Time;
            }

            return true;
        }

        private static List<HeaderHit> ScanHeaders(
            Stream stream,
            Encoding encoding,
            TemplateLogParser parser,
            TemplateParseLayout layout,
            long from,
            long toExclusive,
            CancellationToken cancellationToken,
            out long alignedFrom)
        {
            var hits = new List<HeaderHit>();
            alignedFrom = from;
            if (from >= toExclusive)
                return hits;

            stream.Position = from;
            if (from > 0)
                SkipPartialLine(stream);
            alignedFrom = stream.Position;

            long lineStart = alignedFrom;
            var lineBytes = new List<byte>(256);
            var buf = new byte[65536];
            long length = stream.Length;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                long pos = stream.Position;
                if (pos >= length)
                {
                    if (lineBytes.Count > 0 && lineStart < toExclusive)
                        AddHit(hits, parser, layout, encoding, lineStart, lineBytes);
                    break;
                }

                if (lineStart >= toExclusive && lineBytes.Count == 0)
                    break;

                int n = stream.Read(buf, 0, buf.Length);
                if (n <= 0)
                    break;

                for (int i = 0; i < n; i++)
                {
                    byte b = buf[i];
                    if (b == (byte)'\n')
                    {
                        AddHit(hits, parser, layout, encoding, lineStart, lineBytes);
                        lineBytes.Clear();
                        lineStart = pos + i + 1;
                        if (lineStart >= toExclusive)
                            return hits;
                    }
                    else
                    {
                        lineBytes.Add(b);
                    }
                }
            }

            return hits;
        }

        private static void AddHit(
            List<HeaderHit> hits,
            TemplateLogParser parser,
            TemplateParseLayout layout,
            Encoding encoding,
            long lineStart,
            List<byte> lineBytes)
        {
            int count = lineBytes.Count;
            if (count > 0 && lineBytes[count - 1] == (byte)'\r')
                count--;
            if (count <= 0)
                return;

            string line = encoding.GetString(lineBytes.ToArray(), 0, count);
            if (parser.TryParseHeaderTime(line, layout, out DateTime time))
                hits.Add(new HeaderHit { LineStart = lineStart, Time = time });
        }

        private static void SkipPartialLine(Stream stream)
        {
            long pos = stream.Position;
            if (pos <= 0)
                return;

            stream.Position = pos - 1;
            int prev = stream.ReadByte();
            if (prev == '\n')
                return;

            int b;
            while ((b = stream.ReadByte()) >= 0 && b != '\n')
            {
            }
        }

        private static void Flush(ref LogEntry current, StringBuilder extra, Action<LogEntry> onEntry)
        {
            if (current == null)
                return;

            if (extra.Length > 0)
            {
                current.Message = (current.Message ?? string.Empty) + extra.ToString();
                extra.Clear();
            }

            onEntry(current);
            current = null;
        }
    }
}
