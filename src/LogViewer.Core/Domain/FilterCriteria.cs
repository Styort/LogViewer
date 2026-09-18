using System;
using System.Collections.Generic;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Filter state for log display and Don't Receive (buffer exclusion). No UI types.
    /// </summary>
    public class FilterCriteria
    {
        public LogLevel MinLevel { get; set; } = LogLevel.Trace;

        /// <summary>
        /// Loggers hidden in the list only. Entries stay in the session and reappear when the logger is shown again.
        /// </summary>
        public HashSet<string> ExcludedLoggerFullPaths { get; set; } = new HashSet<string>();

        /// <summary>
        /// Don't Receive: these logger paths (and their descendants) must not be stored in the session at all.
        /// Independent from <see cref="ExcludedLoggerFullPaths"/> so hiding a logger is not the same as dropping it.
        /// </summary>
        public HashSet<string> ExcludedLoggerFullPathsWithBuffer { get; set; } = new HashSet<string>();
        public string SearchText { get; set; } = string.Empty;
        public bool MatchCase { get; set; }
        public bool MatchWholeWord { get; set; }
        public bool UseRegex { get; set; }
        /// <summary>
        /// When true, entry must also satisfy MinLevel to be included during search.
        /// </summary>
        public bool MatchLogLevel { get; set; } = true;
        public bool IsSearchActive { get; set; }
        /// <summary>
        /// Regex compile failed. UI shows an indicator; <c>LogFilter</c> does not apply the search predicate
        /// so the list is not emptied with no explanation.
        /// </summary>
        public bool IsSearchPatternInvalid { get; set; }
        public bool IsTimeIntervalActive { get; set; }
        public DateTime TimeRangeFrom { get; set; }
        public DateTime TimeRangeTo { get; set; }

        /// <summary>
        /// Returns whether <paramref name="entry"/> should be appended to the session buffer.
        /// </summary>
        /// <remarks>
        /// Display filtering uses <see cref="ExcludedLoggerFullPaths"/> in <c>LogFilter.ShouldInclude</c>.
        /// Don't Receive uses <see cref="ExcludedLoggerFullPathsWithBuffer"/> and is applied before
        /// <c>LogSession.AddEntry</c> so the UI never sees the row and <c>EntryCount</c> does not grow.
        /// A match is the entry <c>FullPath</c>, any descendant (<c>excluded + '.' + ...</c>), or the Root node.
        /// </remarks>
        public bool ShouldStoreInBuffer(LogEntry entry)
        {
            if (entry == null)
                return false;
            return ShouldStoreInBuffer(entry.FullPath);
        }

        /// <summary>
        /// Same as <see cref="ShouldStoreInBuffer(LogEntry)"/> for a precomputed logger full path (UI <c>LogMessage.FullPath</c>).
        /// </summary>
        public bool ShouldStoreInBuffer(string fullPath)
        {
            var excluded = ExcludedLoggerFullPathsWithBuffer;
            if (excluded == null || excluded.Count == 0)
                return true;
            // Tree Root is not a real FullPath; Don't Receive on Root drops every source.
            if (excluded.Contains("Root"))
                return false;
            if (string.IsNullOrEmpty(fullPath))
                return true;
            if (excluded.Contains(fullPath))
                return false;
            foreach (var path in excluded)
            {
                if (string.IsNullOrEmpty(path) || path == "Root")
                    continue;
                // Parent Don't Receive also covers loggers that appear later under that node.
                if (fullPath.Length > path.Length
                    && fullPath[path.Length] == '.'
                    && fullPath.StartsWith(path, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }
}
