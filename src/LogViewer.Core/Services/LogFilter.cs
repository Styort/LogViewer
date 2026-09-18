using System;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    public class LogFilter : ILogFilter
    {
        private string _matcherKey;
        private SearchMatcher _cachedMatcher;

        /// <summary>
        /// Whether <paramref name="entry"/> should appear in the filtered list.
        /// </summary>
        /// <remarks>
        /// Predicates are AND, in order: null checks, Don't Show (<see cref="FilterCriteria.ExcludedLoggerFullPaths"/>),
        /// min level, time range if enabled, then search if enabled.
        /// Older code returned immediately after a matching time range, which dropped level and search and looked like
        /// "search broke when the interval was on".
        /// Min level is skipped only while search is active and <see cref="FilterCriteria.MatchLogLevel"/> is false
        /// (same toggle as before). Don't Receive is <see cref="FilterCriteria.ShouldStoreInBuffer"/>, not this method.
        /// Invalid regex: the search predicate is skipped so the list does not go empty without explanation; the UI
        /// shows <see cref="FilterCriteria.IsSearchPatternInvalid"/> instead.
        /// </remarks>
        public bool ShouldInclude(LogEntry entry, FilterCriteria criteria)
        {
            if (entry == null || criteria == null)
                return false;

            // Display only. Don't Receive (ExcludedLoggerFullPathsWithBuffer) is applied in
            // FilterCriteria.ShouldStoreInBuffer before the session stores the entry.
            if (criteria.ExcludedLoggerFullPaths != null && criteria.ExcludedLoggerFullPaths.Contains(entry.FullPath))
                return false;

            bool searchIsConstraining = criteria.IsSearchActive && !criteria.IsSearchPatternInvalid;
            bool applyLevel = !searchIsConstraining || criteria.MatchLogLevel;
            if (applyLevel && !LevelIncluded(criteria.MinLevel, entry.Level))
                return false;

            if (criteria.IsTimeIntervalActive)
            {
                if (entry.Time <= criteria.TimeRangeFrom || entry.Time >= criteria.TimeRangeTo)
                    return false;
            }

            if (searchIsConstraining)
            {
                if (string.IsNullOrEmpty(criteria.SearchText))
                    return true;
                SearchMatcher matcher = GetMatcher(criteria);
                if (matcher.IsPatternInvalid)
                    return true;
                if (!matcher.Matches(entry))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Same field/option matching as <see cref="ShouldInclude"/> search, for find next/prev and the results window.
        /// </summary>
        public static bool MatchesSearch(LogEntry entry, SearchMatcher matcher)
        {
            if (entry == null || matcher == null || matcher.IsPatternInvalid || matcher.IsEmpty)
                return false;
            return matcher.Matches(entry);
        }

        private SearchMatcher GetMatcher(FilterCriteria criteria)
        {
            string key = (criteria.SearchText ?? string.Empty)
                + "\u001f" + criteria.MatchCase
                + "\u001f" + criteria.UseRegex
                + "\u001f" + criteria.MatchWholeWord;
            if (_cachedMatcher == null || _matcherKey != key)
            {
                _cachedMatcher = SearchMatcher.Create(
                    criteria.SearchText,
                    criteria.MatchCase,
                    criteria.UseRegex,
                    criteria.MatchWholeWord);
                _matcherKey = key;
            }
            return _cachedMatcher;
        }

        private static bool LevelIncluded(LogLevel minLevel, LogLevel entryLevel)
        {
            int entry = (int)entryLevel;
            return ((int)minLevel & entry) == entry;
        }
    }
}
