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

        /// <summary>
        /// Drop preset display criteria: Trace, empty search, no interval, all loggers visible.
        /// Don't Receive stays — same rule as ApplyPreset.
        /// </summary>
        public void ClearAppliedPreset()
        {
            _minLevel = eLogLevel.Trace;
            _searchText = string.Empty;
            _matchCase = false;
            _wholeWord = false;
            _regex = false;
            _matchLevel = true;
            _searchActive = false;
            _timeIntervalActive = false;
            _from = default(DateTime);
            _to = default(DateTime);
            RefreshSearchPatternValidity();
            Loggers.SetIncludeOnly(null);
            Loggers.ReplaceDisplayExclusions(null);
            Apply();
        }

        /// <summary>
        /// Snapshot search/interval/tree into a session file. Does not include receivers (those stay in settings.xml).
        /// Checked loggers come from the WPF tree (<paramref name="includedRootsFromTree"/>), not only
        /// Show-only state: unchecking nodes leaves IncludeOnlyPaths empty.
        /// </summary>
        public void CaptureSessionFilter(
            SavedSessionDocument document,
            IEnumerable<string> includedRootsFromTree = null,
            IEnumerable<string> uncheckedFromTree = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            document.MinLevel = FilterPresetMapper.FormatMinLevel((LogLevel)(int)_minLevel);
            document.SearchText = _searchText;
            document.MatchCase = _matchCase;
            document.MatchWholeWord = _wholeWord;
            document.UseRegex = _regex;
            document.MatchLogLevel = _matchLevel;
            document.IsSearchActive = _searchActive;
            document.IsTimeIntervalActive = _timeIntervalActive;
            document.TimeRangeFrom = _from;
            document.TimeRangeTo = _to;
            document.DontReceiveLoggerFullPaths = Loggers.ExcludedWithBufferPaths
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in Loggers.ExcludedPaths)
            {
                if (!string.IsNullOrEmpty(path) && path != "Root")
                    excluded.Add(path);
            }
            if (uncheckedFromTree != null)
            {
                foreach (var path in uncheckedFromTree)
                {
                    if (!string.IsNullOrEmpty(path) && path != "Root")
                        excluded.Add(path);
                }
            }
            document.ExcludedLoggerFullPaths = excluded.ToList();

            // Session keeps FullPath (same buffer, same sources). Do not strip to portable keys —
            // that is for filter presets that must apply to another file.
            List<string> included;
            if (includedRootsFromTree != null)
            {
                included = includedRootsFromTree
                    .Where(p => !string.IsNullOrEmpty(p) && p != "Root")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            else
            {
                included = Loggers.IncludeOnlyPaths
                    .Where(p => !string.IsNullOrEmpty(p) && p != "Root")
                    .ToList();
            }
            document.IncludedLoggerFullPaths = included;
        }

        /// <summary>
        /// Restore search/interval/Don't Show from a session. Pass <paramref name="applyDontReceive"/> false
        /// until the buffer is filled — Don't Receive would skip restored rows on AddEntries.
        /// </summary>
        public void ApplySessionFilter(
            SavedSessionDocument document,
            IEnumerable<string> knownLoggerPaths = null,
            bool applyDontReceive = true)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            _minLevel = (eLogLevel)(int)FilterPresetMapper.ParseMinLevel(document.MinLevel);
            _searchText = document.SearchText ?? string.Empty;
            _matchCase = document.MatchCase;
            _wholeWord = document.MatchWholeWord;
            _regex = document.UseRegex;
            _matchLevel = document.MatchLogLevel;
            _searchActive = document.IsSearchActive;
            _timeIntervalActive = document.IsTimeIntervalActive;
            _from = document.TimeRangeFrom;
            _to = document.TimeRangeTo;
            if (_timeIntervalActive)
                _searchActive = true;

            var preset = new FilterPreset
            {
                IncludedLoggerFullPaths = document.IncludedLoggerFullPaths,
                ExcludedLoggerFullPaths = document.ExcludedLoggerFullPaths
            };
            var included = (document.IncludedLoggerFullPaths ?? new List<string>())
                .Where(p => !string.IsNullOrEmpty(p) && p != "Root")
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            if (document.ExcludedLoggerFullPaths != null)
            {
                foreach (var path in document.ExcludedLoggerFullPaths)
                {
                    if (!string.IsNullOrEmpty(path) && path != "Root")
                        excluded.Add(path);
                }
            }
            if (included.Count > 0)
            {
                foreach (var path in FilterPresetMapper.ComputeExcluded(preset, knownLoggerPaths))
                    excluded.Add(path);
            }
            var dontReceive = applyDontReceive
                ? document.DontReceiveLoggerFullPaths
                : null;

            Loggers.RestoreFromSession(excluded, dontReceive, included);
            Apply();
        }

        /// <summary>
        /// Apply Don't Receive after AddEntries so saved rows stay in the buffer, then live UDP is excluded again.
        /// </summary>
        public void ApplySessionDontReceive(SavedSessionDocument document)
        {
            if (document?.DontReceiveLoggerFullPaths == null)
                return;
            foreach (var path in document.DontReceiveLoggerFullPaths)
            {
                if (!string.IsNullOrEmpty(path))
                    Loggers.DontReceive(path, null);
            }
            Apply();
        }

        /// <summary>
        /// Drop Don't Receive before loading a session file so AddEntries does not skip restored rows.
        /// </summary>
        public void ClearLoggerExclusions()
        {
            Loggers.ClearAll();
            Apply();
        }

        public eLogLevel CurrentMinLevel => _minLevel;
    }
}
