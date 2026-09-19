using System;
using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// The only writer of <see cref="FilterCriteria"/> into the session.
    /// Child VMs change search/level/interval only through this class,
    /// otherwise three independent filters appear again.
    /// </summary>
    public sealed class FilterCoordinator : BaseViewModel
    {
        private readonly LogProcessingService _processing;
        private eLogLevel _minLevel = eLogLevel.Trace;
        private string _searchText = string.Empty;
        private bool _matchCase;
        private bool _wholeWord;
        private bool _regex;
        private bool _matchLevel = true;
        private bool _searchActive;
        private bool _patternInvalid;
        private bool _timeIntervalActive;
        private DateTime _from;
        private DateTime _to;

        public FilterCoordinator(LogProcessingService processing, LoggerFilterState loggers)
        {
            _processing = processing ?? throw new ArgumentNullException(nameof(processing));
            Loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
        }

        /// <summary>Logger exclusions. Same instance the tree mutates — do not replace it.</summary>
        public LoggerFilterState Loggers { get; }

        /// <summary>Invalid regex: toolbar icon, search disabled in criteria, the list is not emptied.</summary>
        public bool IsPatternInvalid
        {
            get => _patternInvalid;
            private set
            {
                if (_patternInvalid == value)
                    return;
                _patternInvalid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Search or time interval is currently applied to the list.</summary>
        public bool IsSearchActive => _searchActive;

        /// <summary>TimeRange* fields in criteria are meaningful only with this flag.</summary>
        public bool IsTimeIntervalActive => _timeIntervalActive;

        /// <summary>Minimum level from the header combo. Apply immediately — level has no debounce.</summary>
        public void SetMinLevel(eLogLevel level)
        {
            _minLevel = level;
            Apply();
        }

        /// <summary>
        /// Update search fields without <see cref="Apply"/>.
        /// While search is off, typing in the toolbar must not rebuild the list on every character.
        /// </summary>
        public void UpdateSearchFields(string text, bool matchCase, bool wholeWord, bool regex, bool matchLevel, bool active)
        {
            _searchText = text ?? string.Empty;
            _matchCase = matchCase;
            _wholeWord = wholeWord;
            _regex = regex;
            _matchLevel = matchLevel;
            _searchActive = active;
            RefreshSearchPatternValidity();
        }

        /// <summary>Search fields plus immediate Apply (Search button / Enter).</summary>
        public void SetSearch(string text, bool matchCase, bool wholeWord, bool regex, bool matchLevel, bool active)
        {
            UpdateSearchFields(text, matchCase, wholeWord, regex, matchLevel, active);
            Apply();
        }

        /// <summary>Recompute the regex icon without Apply. Empty pattern and non-regex are always valid.</summary>
        public void RefreshSearchPatternValidity()
        {
            if (!_regex || string.IsNullOrEmpty(_searchText))
            {
                IsPatternInvalid = false;
                return;
            }

            var matcher = SearchMatcher.Create(_searchText, _matchCase, true, _wholeWord);
            IsPatternInvalid = matcher.IsPatternInvalid;
        }

        /// <summary>Enable interval and search together: without IsSearchActive Core ignores TimeRange*.</summary>
        public void SetTimeInterval(DateTime from, DateTime to)
        {
            _from = from;
            _to = to;
            _timeIntervalActive = true;
            // Interval is a kind of search: without IsSearchActive Core does not apply TimeRange*.
            _searchActive = true;
            Apply();
        }

        /// <summary>Clear the interval, leaving text search as-is.</summary>
        public void ClearTimeInterval()
        {
            _timeIntervalActive = false;
            Apply();
        }

        /// <summary>Turn active search on/off (Search and Clear buttons).</summary>
        public void SetSearchActive(bool active)
        {
            _searchActive = active;
            Apply();
        }

        /// <summary>
        /// Criteria snapshot. HashSets are copied: Core must not see the same collection the UI checkbox mutates.
        /// </summary>
        public FilterCriteria CreateCriteria()
        {
            RefreshSearchPatternValidity();
            // Invalid regex: the list is not emptied; search is simply inactive (icon via IsPatternInvalid).
            bool searchActive = _searchActive && !IsPatternInvalid;
            return new FilterCriteria
            {
                MinLevel = (LogLevel)(int)_minLevel,
                IncludedLoggerFullPaths = new System.Collections.Generic.HashSet<string>(Loggers.IncludeOnlyPaths),
                ExcludedLoggerFullPaths = new System.Collections.Generic.HashSet<string>(Loggers.ExcludedPaths),
                ExcludedLoggerFullPathsWithBuffer = new System.Collections.Generic.HashSet<string>(Loggers.ExcludedWithBufferPaths),
                SearchText = _searchText,
                MatchCase = _matchCase,
                MatchWholeWord = _wholeWord,
                UseRegex = _regex,
                MatchLogLevel = _matchLevel,
                IsSearchActive = searchActive,
                IsSearchPatternInvalid = IsPatternInvalid,
                IsTimeIntervalActive = _timeIntervalActive,
                TimeRangeFrom = _from,
                TimeRangeTo = _to
            };
        }

        /// <summary>The only place FilterCriteria is written into <c>LogProcessingService</c>.</summary>
        public void Apply()
        {
            _processing.SetFilterCriteria(CreateCriteria());
        }

        /// <summary>
        /// Snapshot of the current display filter. Don't Receive is omitted.
        /// Checked loggers are stored as include-roots, not as a dump of every hidden path.
        /// </summary>
        public FilterPreset CapturePreset(string name, IEnumerable<string> knownLoggerPaths, IEnumerable<string> includedRootsFromTree = null)
        {
            var buffer = new HashSet<string>(Loggers.ExcludedWithBufferPaths, StringComparer.Ordinal);
            var excluded = Loggers.ExcludedPaths
                .Where(p => !string.IsNullOrEmpty(p) && p != "Root" && !buffer.Contains(p))
                .ToList();
            List<string> included;
            if (includedRootsFromTree != null)
            {
                included = includedRootsFromTree
                    .Where(p => !string.IsNullOrEmpty(p) && p != "Root")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            else if (Loggers.IncludeOnlyPaths.Count > 0)
            {
                included = FilterPresetMapper.CaptureIncluded(knownLoggerPaths, excluded);
                if (included.Count == 0)
                    included = Loggers.IncludeOnlyPaths.Where(p => !string.IsNullOrEmpty(p) && p != "Root").ToList();
            }
            else
                included = FilterPresetMapper.CaptureIncluded(knownLoggerPaths, excluded);
            included = FilterPresetMapper.ToPortableKeys(included);
            return new FilterPreset
            {
                Name = name ?? string.Empty,
                MinLevel = FilterPresetMapper.FormatMinLevel((LogLevel)(int)_minLevel),
                SearchText = _searchText,
                MatchCase = _matchCase,
                MatchWholeWord = _wholeWord,
                UseRegex = _regex,
                MatchLogLevel = _matchLevel,
                IsSearchActive = _searchActive,
                IsTimeIntervalActive = _timeIntervalActive,
                TimeRangeFrom = _from,
                TimeRangeTo = _to,
                ExcludedLoggerFullPaths = new List<string>(),
                IncludedLoggerFullPaths = included
            };
        }

        /// <summary>
        /// Overwrite search/level/interval/display exclusions from a preset and rebuild the visible list.
        /// Don't Receive is left untouched. Relative From/To are computed from <paramref name="now"/>.
        /// </summary>
        public void ApplyPreset(FilterPreset preset, DateTime now, IEnumerable<string> knownLoggerPaths)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));

            _minLevel = (eLogLevel)(int)FilterPresetMapper.ParseMinLevel(preset.MinLevel);
            _searchText = preset.SearchText ?? string.Empty;
            _matchCase = preset.MatchCase;
            _wholeWord = preset.MatchWholeWord;
            _regex = preset.UseRegex;
            _matchLevel = FilterPresetMapper.ResolveMatchLogLevel(preset);
            _searchActive = preset.IsSearchActive;

            if (!preset.LeaveTimeIntervalUnchanged)
            {
                bool timeActive;
                DateTime from;
                DateTime to;
                FilterPresetMapper.ResolveTimeRange(preset, now, out timeActive, out from, out to);
                _timeIntervalActive = timeActive;
                _from = from;
                _to = to;
                if (_timeIntervalActive)
                    _searchActive = true;
            }

            var included = FilterPresetMapper.ResolveIncluded(preset, knownLoggerPaths);
            var excluded = FilterPresetMapper.ComputeExcluded(preset, knownLoggerPaths);
            Loggers.SetIncludeOnly(included);
            Loggers.ReplaceDisplayExclusions(excluded);
            Apply();
        }

        public eLogLevel CurrentMinLevel => _minLevel;
    }
}
