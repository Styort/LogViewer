using System;
using System.Collections.Generic;
using System.Linq;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Cached field indices and record-boundary markers for a log template.
    /// </summary>
    public sealed class TemplateParseLayout
    {
        private static readonly string[] LevelNames = { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

        public bool IsValid { get; private set; }
        public string Separator { get; private set; }
        public string[] SeparatorAsArray { get; private set; }
        public string[] LogTypeMarkers { get; private set; }
        public int LevelIndex { get; private set; }
        public int LoggerIndex { get; private set; }
        public int MessageIndex { get; private set; }
        public int DateIndex { get; private set; }
        public int ThreadIndex { get; private set; }
        public int ProcessIdIndex { get; private set; }
        public ImportTemplateParameters DateKind { get; private set; }
        public string[] DateFormats { get; private set; }

        public static TemplateParseLayout Create(LogTemplateDto template)
        {
            var layout = new TemplateParseLayout
            {
                DateIndex = -1,
                ThreadIndex = -1,
                ProcessIdIndex = -1,
                LogTypeMarkers = Array.Empty<string>(),
                DateFormats = Array.Empty<string>(),
            };

            if (template?.TemplateParameterses == null)
                return layout;

            var map = template.TemplateParameterses;
            if (!map.ContainsKey(ImportTemplateParameters.level) ||
                !map.ContainsKey(ImportTemplateParameters.logger) ||
                !map.ContainsKey(ImportTemplateParameters.message))
                return layout;

            layout.Separator = template.Separator ?? ";";
            layout.SeparatorAsArray = new[] { layout.Separator };
            layout.LevelIndex = map[ImportTemplateParameters.level];
            layout.LoggerIndex = map[ImportTemplateParameters.logger];
            layout.MessageIndex = map[ImportTemplateParameters.message];

            if (map.ContainsKey(ImportTemplateParameters.threadid))
                layout.ThreadIndex = map[ImportTemplateParameters.threadid];
            if (map.ContainsKey(ImportTemplateParameters.processid))
                layout.ProcessIdIndex = map[ImportTemplateParameters.processid];

            if (map.ContainsKey(ImportTemplateParameters.longdate))
            {
                layout.DateKind = ImportTemplateParameters.longdate;
                layout.DateIndex = map[ImportTemplateParameters.longdate];
                layout.DateFormats = new[]
                {
                    "yyyy-MM-dd HH:mm:ss.ffff",
                    "yy-MM-dd HH:mm:ss.ffff",
                    "yyyy-MM-dd HH:mm:ss.fff",
                    "yy-MM-dd HH:mm:ss.fff",
                };
            }
            else if (map.ContainsKey(ImportTemplateParameters.shortdate))
            {
                layout.DateKind = ImportTemplateParameters.shortdate;
                layout.DateIndex = map[ImportTemplateParameters.shortdate];
                layout.DateFormats = new[] { "yyyy-MM-dd", "yy-MM-dd" };
            }
            else if (map.ContainsKey(ImportTemplateParameters.time))
            {
                layout.DateKind = ImportTemplateParameters.time;
                layout.DateIndex = map[ImportTemplateParameters.time];
                layout.DateFormats = new[] { "HH:mm:ss.ffff", "HH:mm:ss.fff", "HH:mm:ss" };
            }
            else if (map.ContainsKey(ImportTemplateParameters.ticks))
            {
                layout.DateKind = ImportTemplateParameters.ticks;
                layout.DateIndex = map[ImportTemplateParameters.ticks];
            }
            else if (map.ContainsKey(ImportTemplateParameters.date))
            {
                layout.DateKind = ImportTemplateParameters.date;
                layout.DateIndex = map[ImportTemplateParameters.date];
                layout.DateFormats = new[]
                {
                    "yyyy-MM-dd HH:mm:ss.ffff",
                    "yy-MM-dd HH:mm:ss.ffff",
                    "yyyy-MM-dd",
                    "yy-MM-dd",
                };
            }

            layout.LogTypeMarkers = BuildLogTypeMarkers(layout.LevelIndex, map.Values.Max(), layout.Separator);
            layout.IsValid = true;
            return layout;
        }

        private static string[] BuildLogTypeMarkers(int levelIdx, int maxIdx, string sep)
        {
            var list = new List<string>(LevelNames.Length * 2);
            foreach (var level in LevelNames)
            {
                AddMarker(list, levelIdx, maxIdx, sep, level);
                var upper = level.ToUpperInvariant();
                if (upper != level)
                    AddMarker(list, levelIdx, maxIdx, sep, upper);
            }
            return list.ToArray();
        }

        private static void AddMarker(List<string> list, int levelIdx, int maxIdx, string sep, string level)
        {
            if (levelIdx == 0)
            {
                list.Add(level + sep);
                list.Add(sep + level);
            }
            else if (levelIdx == maxIdx)
                list.Add(level + sep);
            else
                list.Add(sep + level + sep);
        }
    }
}
