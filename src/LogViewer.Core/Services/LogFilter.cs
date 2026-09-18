using System;
using System.Text.RegularExpressions;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.Services
{
    public class LogFilter : ILogFilter
    {
        public bool ShouldInclude(LogEntry entry, FilterCriteria criteria)
        {
            if (entry == null || criteria == null) return false;
            // Display only. Don't Receive (ExcludedLoggerFullPathsWithBuffer) is applied in
            // FilterCriteria.ShouldStoreInBuffer before the session stores the entry.
            if (criteria.ExcludedLoggerFullPaths != null && criteria.ExcludedLoggerFullPaths.Contains(entry.FullPath))
                return false;

            if (criteria.IsTimeIntervalActive)
            {
                if (entry.Time <= criteria.TimeRangeFrom || entry.Time >= criteria.TimeRangeTo)
                    return false;
                return true;
            }

            if (criteria.IsSearchActive)
            {
                bool levelOk = !criteria.MatchLogLevel || LevelIncluded(criteria.MinLevel, entry.Level);
                bool searchOk = string.IsNullOrEmpty(criteria.SearchText) || MessageMatchesSearch(
                    entry.Message, criteria.SearchText, criteria.MatchCase, criteria.UseRegex, criteria.MatchWholeWord);
                return levelOk && searchOk;
            }

            return LevelIncluded(criteria.MinLevel, entry.Level);
        }

        private static bool LevelIncluded(LogLevel minLevel, LogLevel entryLevel)
        {
            int entry = (int)entryLevel;
            return ((int)minLevel & entry) == entry;
        }

        private static bool MessageMatchesSearch(string message, string searchText, bool matchCase, bool useRegex, bool matchWholeWord)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(searchText)) return false;

            if (useRegex)
            {
                var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                try
                {
                    return Regex.IsMatch(message, searchText, options);
                }
                catch
                {
                    return false;
                }
            }

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (matchWholeWord)
            {
                return message.IndexOf($" {searchText} ", comparison) >= 0
                    || message.StartsWith(searchText + " ", comparison)
                    || message.EndsWith(" " + searchText, comparison)
                    || (message.StartsWith(searchText, comparison) && message.EndsWith(searchText, comparison));
            }
            return message.IndexOf(searchText, comparison) >= 0;
        }
    }
}
