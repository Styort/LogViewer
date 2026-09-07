using System;
using System.Collections.Generic;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Filter state for log display. No UI types.
    /// </summary>
    public class FilterCriteria
    {
        public LogLevel MinLevel { get; set; } = LogLevel.Trace;
        public HashSet<string> ExcludedLoggerFullPaths { get; set; } = new HashSet<string>();
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
        public bool IsTimeIntervalActive { get; set; }
        public DateTime TimeRangeFrom { get; set; }
        public DateTime TimeRangeTo { get; set; }
    }
}
