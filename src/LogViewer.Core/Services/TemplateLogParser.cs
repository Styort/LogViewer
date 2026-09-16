using System;
using System.Collections.Generic;
using System.Globalization;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Parses a file-log header line using a cached template layout. Continuation lines are appended separately.
    /// </summary>
    public class TemplateLogParser
    {
        private static readonly Dictionary<string, LogLevel> LogLevelMapping = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase)
        {
            { "Trace", LogLevel.Trace },
            { "Debug", LogLevel.Debug },
            { "Info", LogLevel.Info },
            { "Warn", LogLevel.Warn },
            { "Error", LogLevel.Error },
            { "Fatal", LogLevel.Fatal },
        };

        /// <summary>
        /// Parses the first line of a log record. Returns null if the line or layout is invalid.
        /// </summary>
        public LogEntry ParseHeaderLine(string line, TemplateParseLayout layout, string address)
        {
            if (string.IsNullOrEmpty(line) || layout == null || !layout.IsValid)
                return null;

            var parts = line.Split(layout.SeparatorAsArray, StringSplitOptions.None);

            string message;
            int msgIdx = layout.MessageIndex;
            if (msgIdx >= parts.Length)
                message = string.Empty;
            else if (msgIdx == parts.Length - 1)
                message = parts[msgIdx];
            else
                message = string.Join(layout.Separator, parts, msgIdx, parts.Length - msgIdx);

            DateTime date = DateTime.Now;
            if (layout.DateIndex >= 0 && layout.DateIndex < parts.Length &&
                !TryParseDate(parts[layout.DateIndex], layout, out date))
                date = DateTime.Now;

            LogLevel level = LogLevel.Info;
            if (layout.LevelIndex < parts.Length)
            {
                string levelStr = parts[layout.LevelIndex];
                if (!string.IsNullOrEmpty(levelStr) && !LogLevelMapping.TryGetValue(levelStr, out level))
                    level = LogLevel.Info;
            }

            string logger = layout.LoggerIndex < parts.Length ? parts[layout.LoggerIndex] : string.Empty;

            int thread = -1;
            if (layout.ThreadIndex >= 0 && layout.ThreadIndex < parts.Length)
                int.TryParse(parts[layout.ThreadIndex], out thread);

            int? processId = null;
            if (layout.ProcessIdIndex >= 0 && layout.ProcessIdIndex < parts.Length &&
                int.TryParse(parts[layout.ProcessIdIndex], out int pid))
                processId = pid;

            return new LogEntry
            {
                Address = address,
                Time = date,
                Level = level,
                Logger = logger,
                Message = message,
                Thread = thread,
                ProcessID = processId,
            };
        }

        /// <summary>
        /// Parses the timestamp of a header line. Returns false if the line is not a header or the date cannot be parsed.
        /// </summary>
        public bool TryParseHeaderTime(string line, TemplateParseLayout layout, out DateTime time)
        {
            time = default(DateTime);
            if (string.IsNullOrEmpty(line) || layout == null || !layout.IsValid || layout.DateIndex < 0)
                return false;
            if (!StringUtils.ContainsAnyOf(line, layout.LogTypeMarkers))
                return false;

            var parts = line.Split(layout.SeparatorAsArray, StringSplitOptions.None);
            if (layout.DateIndex >= parts.Length)
                return false;

            return TryParseDate(parts[layout.DateIndex], layout, out time);
        }

        private static bool TryParseDate(string dateStr, TemplateParseLayout layout, out DateTime date)
        {
            date = default(DateTime);
            if (string.IsNullOrEmpty(dateStr) || layout == null)
                return false;
            if (dateStr.IndexOf('\0') >= 0)
                dateStr = dateStr.Replace("\0", "");

            if (layout.DateKind == ImportTemplateParameters.ticks)
            {
                if (!long.TryParse(dateStr, out long ticks))
                    return false;
                try
                {
                    date = new DateTime(ticks);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
            }

            if (layout.DateFormats != null && layout.DateFormats.Length > 0)
                return DateTime.TryParseExact(dateStr, layout.DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

            return DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
