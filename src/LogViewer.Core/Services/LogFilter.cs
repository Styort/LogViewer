using System;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    public class LogFilter : ILogFilter
    {
        private volatile MatcherCache _matcherCache;

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
            if (!LoggerIsShown(entry.FullPath, criteria))
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

        internal SearchMatcher GetMatcher(FilterCriteria criteria)
        {
            // Receive threads and the background refilter call this concurrently. Options and matcher are
            // published as one immutable object so a reader never pairs one set of options with another
            // matcher. Options are compared field by field: a string key built per call allocated once per
            // entry, a million strings for one refilter of a large buffer.
            var cache = _matcherCache;
            if (cache == null || !cache.IsFor(criteria))
            {
                cache = new MatcherCache(criteria, SearchMatcher.Create(
                    criteria.SearchText,
                    criteria.MatchCase,
                    criteria.UseRegex,
                    criteria.MatchWholeWord));
                _matcherCache = cache;
            }
            return cache.Matcher;
        }

        private sealed class MatcherCache
        {
            private readonly string _searchText;
            private readonly bool _matchCase;
            private readonly bool _useRegex;
            private readonly bool _matchWholeWord;

            public MatcherCache(FilterCriteria criteria, SearchMatcher matcher)
            {
                _searchText = criteria.SearchText ?? string.Empty;
                _matchCase = criteria.MatchCase;
                _useRegex = criteria.UseRegex;
                _matchWholeWord = criteria.MatchWholeWord;
                Matcher = matcher;
            }

            public SearchMatcher Matcher { get; }

            public bool IsFor(FilterCriteria criteria)
            {
                return _matchCase == criteria.MatchCase
                    && _useRegex == criteria.UseRegex
                    && _matchWholeWord == criteria.MatchWholeWord
                    && string.Equals(_searchText, criteria.SearchText ?? string.Empty, StringComparison.Ordinal);
            }
        }

        private static bool LoggerIsShown(string fullPath, FilterCriteria criteria)
        {
            var included = criteria.IncludedLoggerFullPaths;
            if (included != null && included.Count > 0)
                return FilterPresetMapper.IsKeptByIncludeOnly(fullPath, included);

            var excluded = criteria.ExcludedLoggerFullPaths;
            if (excluded == null || excluded.Count == 0)
                return true;
            if (string.IsNullOrEmpty(fullPath))
                return true;
            return !excluded.Contains(fullPath);
        }

        private static bool LevelIncluded(LogLevel minLevel, LogLevel entryLevel)
        {
            int entry = (int)entryLevel;
            return ((int)minLevel & entry) == entry;
        }
    }
}
