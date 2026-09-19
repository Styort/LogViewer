using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.State;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Maps a <see cref="FilterPreset"/> onto display-filter state (not Don't Receive, not the buffer).
    /// </summary>
    public static class FilterPresetMapper
    {
        /// <summary>Min level from XML. Unknown or empty names become Trace (safe default for old files).</summary>
        public static LogLevel ParseMinLevel(string name)
        {
            if (string.IsNullOrEmpty(name))
                return LogLevel.Trace;
            LogLevel parsed;
            if (Enum.TryParse(name, true, out parsed) && Enum.IsDefined(typeof(LogLevel), parsed))
                return parsed;
            return LogLevel.Trace;
        }

        public static string FormatMinLevel(LogLevel level)
        {
            return level.ToString();
        }

        public static bool ResolveMatchLogLevel(FilterPreset preset)
        {
            if (preset == null || !preset.MatchLogLevel.HasValue)
                return true;
            return preset.MatchLogLevel.Value;
        }

        /// <summary>
        /// Highest visible FullPaths: checked loggers whose parent is hidden. Compact so a "only Ping + Navigator"
        /// preset stores two paths instead of every other logger in the file.
        /// Empty when nothing is hidden (show the whole tree).
        /// </summary>
        public static List<string> CaptureIncluded(IEnumerable<string> knownLoggerPaths, IEnumerable<string> excludedLoggerFullPaths)
        {
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            if (excludedLoggerFullPaths != null)
            {
                foreach (var path in excludedLoggerFullPaths)
                {
                    if (!string.IsNullOrEmpty(path) && path != "Root")
                        excluded.Add(path);
                }
            }

            if (excluded.Count == 0)
                return new List<string>();

            var visible = new List<string>();
            if (knownLoggerPaths != null)
            {
                foreach (var known in knownLoggerPaths)
                {
                    if (string.IsNullOrEmpty(known) || known == "Root")
                        continue;
                    if (!excluded.Contains(known))
                        visible.Add(known);
                }
            }

            // Mixed parents (file source with some children hidden) stay in `visible` because
            // they are not in the exclude set. They must not become include-roots: IsInSubtree
            // would then keep every logger under that file (`2026-09-18.txt.SecurityLog` matches
            // prefix `…\2026-09-18.txt`).
            var fullyVisible = new List<string>();
            foreach (var path in visible)
            {
                if (!HasExcludedDescendant(path, excluded, knownLoggerPaths))
                    fullyVisible.Add(path);
            }

            var compact = new List<string>();
            foreach (var path in fullyVisible)
            {
                bool descendantOfVisible = false;
                foreach (var other in fullyVisible)
                {
                    if (other == path)
                        continue;
                    if (LoggerFilterState.IsInSubtree(path, other))
                    {
                        descendantOfVisible = true;
                        break;
                    }
                }
                if (!descendantOfVisible)
                    compact.Add(path);
            }

            return ToPortableKeys(compact);
        }

        /// <summary>
        /// Included roots to apply. Prefers the compact list from XML; old files with only exclusions
        /// are converted using currently known loggers (after import the tree is already built).
        /// </summary>
        public static List<string> ResolveIncluded(FilterPreset preset, IEnumerable<string> knownLoggerPaths)
        {
            if (preset?.IncludedLoggerFullPaths != null)
            {
                var fromFile = new List<string>();
                foreach (var path in preset.IncludedLoggerFullPaths)
                {
                    if (!string.IsNullOrEmpty(path) && path != "Root")
                        fromFile.Add(path);
                }
                fromFile = ToPortableKeys(fromFile);
                if (fromFile.Count > 0)
                    return fromFile;
            }

            return CaptureIncluded(knownLoggerPaths, preset?.ExcludedLoggerFullPaths);
        }

        /// <summary>
        /// Display exclusions after Apply. When include-roots exist, hide every known path outside those subtrees
        /// and ignore the legacy excluded dump (parents in that dump must not hide a checked child).
        /// </summary>
        public static HashSet<string> ComputeExcluded(FilterPreset preset, IEnumerable<string> knownLoggerPaths)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (preset == null)
                return result;

            var included = ResolveIncluded(preset, knownLoggerPaths);
            if (included.Count > 0)
            {
                if (knownLoggerPaths == null)
                    return result;
                foreach (var known in knownLoggerPaths)
                {
                    if (string.IsNullOrEmpty(known) || known == "Root")
                        continue;
                    if (!IsKeptByIncludeOnly(known, included))
                        result.Add(known);
                }
                return result;
            }

            if (preset.ExcludedLoggerFullPaths != null)
            {
                foreach (var path in preset.ExcludedLoggerFullPaths)
                {
                    if (!string.IsNullOrEmpty(path) && path != "Root")
                        result.Add(path);
                }
            }

            return result;
        }

        private static bool HasExcludedDescendant(
            string path,
            HashSet<string> excluded,
            IEnumerable<string> knownLoggerPaths)
        {
            if (knownLoggerPaths == null)
                return false;
            foreach (var known in knownLoggerPaths)
            {
                if (string.IsNullOrEmpty(known) || known == path)
                    continue;
                if (LoggerFilterState.IsInSubtree(known, path) && excluded.Contains(known))
                    return true;
            }
            return false;
        }

        public static bool IsKeptByIncludeOnly(string fullPath, IEnumerable<string> included)
        {
            if (string.IsNullOrEmpty(fullPath) || included == null)
                return false;
            foreach (var inc in included)
            {
                if (LoggerFilterState.MatchesIncludeRoot(fullPath, inc))
                    return true;
            }
            return false;
        }

        /// <summary>Drop file/IP prefixes so a preset survives a different import path.</summary>
        public static List<string> ToPortableKeys(IEnumerable<string> paths)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (paths == null)
                return result;
            foreach (var path in paths)
            {
                string key = LoggerFilterState.ToPortableLoggerKey(path);
                if (string.IsNullOrEmpty(key) || key == "Root")
                    continue;
                if (seen.Add(key))
                    result.Add(key);
            }
            return result;
        }

        /// <summary>
        /// Resolves the interval that Apply should write.
        /// Relative: From/To are computed from <paramref name="now"/> (local clock, same as the interval dialog).
        /// </summary>
        public static void ResolveTimeRange(FilterPreset preset, DateTime now, out bool active, out DateTime from, out DateTime to)
        {
            active = false;
            from = default(DateTime);
            to = default(DateTime);
            if (preset == null || preset.LeaveTimeIntervalUnchanged)
                return;

            if (preset.IsRelativeTimeInterval)
            {
                int minutes = preset.RelativeMinutes > 0 ? preset.RelativeMinutes : 15;
                active = true;
                to = now;
                from = now.AddMinutes(-minutes);
                return;
            }

            active = preset.IsTimeIntervalActive;
            from = preset.TimeRangeFrom;
            to = preset.TimeRangeTo;
        }
    }
}
