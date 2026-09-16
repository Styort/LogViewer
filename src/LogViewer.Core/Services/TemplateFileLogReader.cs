using System;
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

        public static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize);
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
            using (var reader = new StreamReader(stream, encoding, detectBom, BufferSize, leaveOpen: true))
            {
                LogEntry current = null;
                var extra = new StringBuilder();
                long lastReportedPos = stream.CanSeek ? stream.Position : 0;
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

                    if (onProgress != null && stream.CanSeek && stream.Length > 0 && stream.Position - lastReportedPos > 65536)
                    {
                        lastReportedPos = stream.Position;
                        onProgress(stream.Position, stream.Length);
                    }
                }

                Flush(ref current, extra, onEntry);
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
