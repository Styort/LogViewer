using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Parses a single line from a file log using a template. No UI.
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
        /// Parses a line into a LogEntry. Returns null if line is empty or template is invalid for this line.
        /// </summary>
        public LogEntry ParseLine(string line, LogTemplateDto template, string address)
        {
            if (string.IsNullOrEmpty(line)) return null;
            if (template?.TemplateParameterses == null) return null;
            if (!template.TemplateParameterses.ContainsKey(ImportTemplateParameters.level) ||
                !template.TemplateParameterses.ContainsKey(ImportTemplateParameters.logger) ||
                !template.TemplateParameterses.ContainsKey(ImportTemplateParameters.message))
                return null;

            var parts = line.Split(new[] { template.Separator }, StringSplitOptions.None);
            int msgIdx = template.TemplateParameterses[ImportTemplateParameters.message];
            var message = new StringBuilder();
            for (int i = msgIdx; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                    message.Append(parts[i]);
            }

            ImportTemplateParameters dateFormat = ImportTemplateParameters.date;
            if (template.TemplateParameterses.ContainsKey(ImportTemplateParameters.longdate))
                dateFormat = ImportTemplateParameters.longdate;
            else if (template.TemplateParameterses.ContainsKey(ImportTemplateParameters.shortdate))
                dateFormat = ImportTemplateParameters.shortdate;
            else if (template.TemplateParameterses.ContainsKey(ImportTemplateParameters.time))
                dateFormat = ImportTemplateParameters.time;
            else if (template.TemplateParameterses.ContainsKey(ImportTemplateParameters.ticks))
                dateFormat = ImportTemplateParameters.ticks;

            DateTime date = DateTime.Now;
            if (template.TemplateParameterses.ContainsKey(dateFormat) && template.TemplateParameterses[dateFormat] < parts.Length)
            {
                string dateStr = parts[template.TemplateParameterses[dateFormat]].Replace("\0", "");
                if (dateFormat == ImportTemplateParameters.ticks)
                {
                    if (long.TryParse(dateStr, out long ticks))
                        date = new DateTime(ticks);
                }
                else
                {
                    if (!DateTime.TryParse(dateStr, out date) &&
                        !DateTime.TryParseExact(dateStr, "yy-MM-dd HH:mm:ss.ffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        date = DateTime.Now;
                }
            }

            int levelIdx = template.TemplateParameterses[ImportTemplateParameters.level];
            string levelStr = levelIdx < parts.Length ? StringUtils.ToPascalCase(parts[levelIdx]) : "Info";
            LogLevel level = LogLevelMapping.TryGetValue(levelStr, out var lvl) ? lvl : LogLevel.Info;

            int loggerIdx = template.TemplateParameterses[ImportTemplateParameters.logger];
            string logger = loggerIdx < parts.Length ? parts[loggerIdx] : string.Empty;

            int thread = -1;
            if (template.TemplateParameterses.ContainsKey(ImportTemplateParameters.threadid))
            {
                int tidx = template.TemplateParameterses[ImportTemplateParameters.threadid];
                if (tidx < parts.Length)
                    int.TryParse(parts[tidx], out thread);
            }

            int? processId = null;
            if (template.TemplateParameterses.ContainsKey(ImportTemplateParameters.processid))
            {
                int pidx = template.TemplateParameterses[ImportTemplateParameters.processid];
                if (pidx < parts.Length && int.TryParse(parts[pidx], out int pid))
                    processId = pid;
            }

            return new LogEntry
            {
                Address = address,
                Time = date,
                Level = level,
                Logger = logger,
                Message = message.ToString(),
                Thread = thread,
                ProcessID = processId,
            };
        }
    }
}
