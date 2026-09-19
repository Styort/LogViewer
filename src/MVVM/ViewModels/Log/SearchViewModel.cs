using System;
using System.Collections.Generic;
using System.Linq;
using LogViewer.Adapters;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.Services;
using NLog;
using LogLevel = LogViewer.Core.Domain.LogLevel;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Search, Find Next/Prev, jump by level, and time interval.
    /// Only <see cref="FilterCoordinator"/> writes the list filter — this VM has no copy of the AND logic.
    /// </summary>
    public sealed class SearchViewModel : BaseViewModel, IResettable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly LogViewState _state;
        private readonly FilterCoordinator _filter;
        private readonly LogSession _session;
        private readonly LogProcessingService _processing;
        private readonly LogEntryProjector _projector;
        private readonly LogQueryService _query;
        private readonly IDialogService _dialogs;
        private readonly IAppSettings _settings;
        private readonly Func<eLogLevel> _minLevel;

        private bool _isSearchProcess;
        private bool _suppressCoordinatorSync;
        private bool _clearSearchResultIsEnabled;
        private bool _isMatchCase;
        private bool _isMatchWholeWord;
        private bool _isMatchLogLevel = true;
        private bool _useRegularExpressions;
        private bool _isEnableFindPrevious;
        private string _searchText = string.Empty;
        private string _highlightSearchText = string.Empty;

        // Find Next/Prev and jump-by-level are pressed in a row; the list does not change.
        // Cache LogEntry so Properties are not cloned on every keypress. Cleared
        // when the Logs reference or item count changes.
        private IList<LogMessage> _cachedViewSource;
        private int _cachedViewCount;
        private List<LogEntry> _cachedView;

        private RelayCommand _searchCommand;
        private RelayCommand _findNextCommand;
        private RelayCommand _findPreviousCommand;
        private RelayCommand _findNextWarningCommand;
        private RelayCommand _findNextErrorCommand;
        private RelayCommand _clearCommand;
        private RelayCommand _goToTimestampCommand;
        private RelayCommand _setTimeIntervalCommand;

        public SearchViewModel(
            LogViewState state,
            FilterCoordinator filter,
            LogSession session,
            LogProcessingService processing,
            LogEntryProjector projector,
            LogQueryService query,
            IDialogService dialogs,
            IAppSettings settings,
            Func<eLogLevel> minLevel)
        {
            _state = state;
            _filter = filter;
            _session = session;
            _processing = processing;
            _projector = projector;
            _query = query;
            _dialogs = dialogs;
            _settings = settings;
            _minLevel = minLevel;
            _state.SelectedLogChanged += (sender, args) => RefreshFindPreviousEnabled();
            _filter.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(FilterCoordinator.IsPatternInvalid))
                    OnPropertyChanged(nameof(IsSearchPatternInvalid));
            };
        }

        /// <summary>Search is active: the list is narrowed, Clear in the toolbar is enabled.</summary>
        public bool IsSearchProcess
        {
            get => _isSearchProcess;
            set
            {
                _isSearchProcess = value;
                ClearSearchResultIsEnabled = _isSearchProcess || SearchText.Length > 0;
                OnPropertyChanged();
            }
        }

        /// <summary>Clear-search button: visible while there is text or an active filter.</summary>
        public bool ClearSearchResultIsEnabled
        {
            get => _clearSearchResultIsEnabled;
            set
            {
                _clearSearchResultIsEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Match case. Updates the matcher immediately; Apply only if search is already on.</summary>
        public bool IsMatchCase
        {
            get => _isMatchCase;
            set
            {
                _isMatchCase = value;
                OnSearchOptionsChanged();
                OnPropertyChanged();
            }
        }

        /// <summary>Whole word. Same as match case: Apply only while search is active.</summary>
        public bool IsMatchWholeWord
        {
            get => _isMatchWholeWord;
            set
            {
                _isMatchWholeWord = value;
                OnSearchOptionsChanged();
                OnPropertyChanged();
            }
        }

        /// <summary>Search only among rows at the current minimum level, not the whole buffer.</summary>
        public bool IsMatchLogLevel
        {
            get => _isMatchLogLevel;
            set
            {
                _isMatchLogLevel = value;
                OnSearchOptionsChanged();
                OnPropertyChanged();
            }
        }

        /// <summary>Regex. An invalid pattern sets <see cref="IsSearchPatternInvalid"/>; the list is left alone.</summary>
        public bool UseRegularExpressions
        {
            get => _useRegularExpressions;
            set
            {
                _useRegularExpressions = value;
                OnSearchOptionsChanged();
                OnPropertyChanged();
            }
        }

        /// <summary>Coordinator proxy: icon in the search box.</summary>
        public bool IsSearchPatternInvalid => _filter.IsPatternInvalid;

        /// <summary>Toolbar text. While search is off — regex validity only, no Apply.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                ClearSearchResultIsEnabled = IsSearchProcess || SearchText.Length > 0;
                RefreshFindPreviousEnabled();
                SyncSearchToCoordinator(apply: false);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearchPatternInvalid));
            }
        }

        /// <summary>Highlight in the Message cell. Differs from SearchText until the user presses Find.</summary>
        public string HighlightSearchText
        {
            get => _highlightSearchText;
            set
            {
                _highlightSearchText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Find Previous is meaningless without a selected row (nowhere to go back).</summary>
        public bool IsEnableFindPrevious
        {
            get => _isEnableFindPrevious;
            set
            {
                _isEnableFindPrevious = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Enter / Search button: enable the list filter.</summary>
        public RelayCommand SearchLogCommand => _searchCommand ?? (_searchCommand = new RelayCommand(Search));
        /// <summary>Next match with wrap, same as the old Find Next.</summary>
        public RelayCommand FindNextCommand => _findNextCommand ?? (_findNextCommand = new RelayCommand(FindNext));
        /// <summary>Previous match without wrap.</summary>
        public RelayCommand FindPreviousCommand => _findPreviousCommand ?? (_findPreviousCommand = new RelayCommand(FindPrevious));
        public RelayCommand FindNextWarningCommand => _findNextWarningCommand ?? (_findNextWarningCommand = new RelayCommand(FindNextWarning));
        public RelayCommand FindNextErrorCommand => _findNextErrorCommand ?? (_findNextErrorCommand = new RelayCommand(FindNextError));
        public RelayCommand ClearSearchResultCommand => _clearCommand ?? (_clearCommand = new RelayCommand(ClearSearchResult));
        public RelayCommand GoToTimestampCommand => _goToTimestampCommand ?? (_goToTimestampCommand = new RelayCommand(GoToTimestamp));
        public RelayCommand SetTimeIntervalCommand => _setTimeIntervalCommand ?? (_setTimeIntervalCommand = new RelayCommand(SetTimeInterval));

        /// <summary>Copy preset search fields into the toolbar. Coordinator already called Apply.</summary>
        public void LoadFromPreset(string text, bool matchCase, bool wholeWord, bool regex, bool matchLevel, bool searchActive)
        {
            _suppressCoordinatorSync = true;
            try
            {
                _searchText = text ?? string.Empty;
                _isMatchCase = matchCase;
                _isMatchWholeWord = wholeWord;
                _useRegularExpressions = regex;
                _isMatchLogLevel = matchLevel;
                _isSearchProcess = searchActive;
                _highlightSearchText = searchActive ? _searchText : string.Empty;
                ClearSearchResultIsEnabled = _isSearchProcess || _searchText.Length > 0;
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(IsMatchCase));
                OnPropertyChanged(nameof(IsMatchWholeWord));
                OnPropertyChanged(nameof(UseRegularExpressions));
                OnPropertyChanged(nameof(IsMatchLogLevel));
                OnPropertyChanged(nameof(IsSearchProcess));
                OnPropertyChanged(nameof(HighlightSearchText));
                OnPropertyChanged(nameof(IsSearchPatternInvalid));
                RefreshFindPreviousEnabled();
            }
            finally
            {
                _suppressCoordinatorSync = false;
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            SearchText = string.Empty;
            HighlightSearchText = string.Empty;
            IsSearchProcess = false;
            _filter.ClearTimeInterval();
            _filter.SetSearch(string.Empty, _isMatchCase, _isMatchWholeWord, _useRegularExpressions, _isMatchLogLevel, false);
        }

        private void Search(object obj)
        {
            Logger.Debug($"Search with {SearchText}");
            if (_state.IsBusy) return;

            SyncSearchToCoordinator(apply: false);
            if (IsSearchPatternInvalid)
                return;

            if (string.IsNullOrEmpty(SearchText))
            {
                ClearSearchResult();
                return;
            }

            _state.CaptureSelectionAnchor();
            bool isOpenInAnotherWinow = (bool)obj;

            if (isOpenInAnotherWinow)
            {
                var searchCriteria = _filter.CreateCriteria();
                searchCriteria.IsSearchActive = true;
                var entries = _session.GetAllEntries()
                    .Where(e => _processing.Filter.ShouldInclude(e, searchCriteria))
                    .ToList();
                var logMessages = entries.Select(e => _projector.Project(e)).Where(m => m != null).ToList();
                if (logMessages.Any())
                    _dialogs.ShowSearchResults(logMessages, SearchText, IsMatchCase, UseRegularExpressions, IsMatchWholeWord, m => _state.SelectedLog = m);
                else
                    _dialogs.ShowInformation(Locals.NothingFoundMessageBoxInfo, Locals.Search);
            }
            else
            {
                IsSearchProcess = true;
                SyncSearchToCoordinator(apply: true);
                HighlightSearchText = SearchText;
            }
        }

        private void FindNext()
        {
            if (_state.IsBusy) return;
            try
            {
                if (string.IsNullOrEmpty(SearchText) && _state.SelectedLog != null)
                {
                    SearchText = _state.SelectedLog.Message;
                    HighlightSearchText = SearchText;
                }
                if (string.IsNullOrEmpty(SearchText)) return;
                SyncSearchToCoordinator(apply: false);
                if (IsSearchPatternInvalid)
                    return;

                HighlightSearchText = SearchText;
                var index = Query(findNext: true);
                if (index >= 0)
                    _state.SelectedLog = _state.Logs[index];
            }
            catch (Exception e)
            {
                Logger.Warn(e, "An error occurred while FindNext");
            }
        }

        private void FindPrevious()
        {
            if (_state.IsBusy) return;
            try
            {
                if (string.IsNullOrEmpty(SearchText) && _state.SelectedLog != null)
                {
                    SearchText = _state.SelectedLog.Message;
                    HighlightSearchText = SearchText;
                }
                if (string.IsNullOrEmpty(SearchText)) return;
                SyncSearchToCoordinator(apply: false);
                if (IsSearchPatternInvalid)
                    return;

                HighlightSearchText = SearchText;
                var index = Query(findNext: false);
                if (index >= 0)
                    _state.SelectedLog = _state.Logs[index];
            }
            catch (Exception e)
            {
                Logger.Warn(e, "An error occurred while FindPrevious");
            }
        }

        private int Query(bool findNext)
        {
            var view = GetViewEntries();
            int from = _state.SelectedLog == null ? -1 : _state.Logs.IndexOf(_state.SelectedLog);
            var matcher = SearchMatcher.Create(SearchText, IsMatchCase, UseRegularExpressions, IsMatchWholeWord);
            var min = IsMatchLogLevel ? (LogLevel)(int)_minLevel() : LogLevel.Trace;
            return findNext
                ? _query.FindNext(view, from, matcher, min)
                : _query.FindPrevious(view, from, matcher, min);
        }

        private void FindNextWarning()
        {
            JumpByLevel(LogLevel.Warn);
        }

        private void FindNextError()
        {
            JumpByLevel(LogLevel.Error);
        }

        private void JumpByLevel(LogLevel level)
        {
            var view = GetViewEntries();
            int from = _state.SelectedLog == null ? -1 : _state.Logs.IndexOf(_state.SelectedLog);
            int index = _query.FindNextByLevel(view, from, level);
            if (index >= 0)
                _state.SelectedLog = _state.Logs[index];
        }

        private void ClearSearchResult()
        {
            if (_state.IsBusy) return;

            if (IsSearchProcess)
            {
                IsSearchProcess = false;
                _filter.ClearTimeInterval();
                _filter.SetSearch(SearchText, IsMatchCase, IsMatchWholeWord, UseRegularExpressions, IsMatchLogLevel, false);
                var filtered = _session.GetFilteredEntries(_processing.Filter);
                _state.Logs = new AsyncObservableCollection<LogMessage>(filtered.Select(e => _projector.Project(e)).Where(m => m != null));
                _state.SelectedLog = _state.GetLastSelectedOrNearby();
            }

            SearchText = string.Empty;
            HighlightSearchText = string.Empty;
        }

        private void GoToTimestamp()
        {
            if (_state.IsBusy) return;

            var picked = _dialogs.SelectTimestamp(_state.SelectedLog?.Time);
            if (!picked.HasValue)
                return;

            var goTo = picked.Value;
            TimeSpan truncateValue = TimeSpan.FromMinutes(1);
            if (goTo.Second != 0)
                truncateValue = TimeSpan.FromSeconds(1);
            if (goTo.Millisecond != 0)
                truncateValue = TimeSpan.FromMilliseconds(1);

            var view = GetViewEntries();
            int index = _query.FindByTimestamp(view, goTo, truncateValue);
            if (index >= 0)
                _state.SelectedLog = _state.Logs[index];
            else
                _dialogs.ShowInformation(string.Format(Locals.NotFoundAnyMessagesWithDateMessageBoxInfo, goTo.ToString(_settings.DataFormat)));
        }

        private void SetTimeInterval()
        {
            if (_state.IsBusy) return;

            var result = _dialogs.SelectTimeInterval(_state.SelectedLog?.Time);
            if (!result.Confirmed)
                return;

            IsSearchProcess = true;
            _filter.SetTimeInterval(result.From, result.To);
        }

        private void OnSearchOptionsChanged()
        {
            if (_suppressCoordinatorSync)
                return;
            SyncSearchToCoordinator(apply: IsSearchProcess);
            OnPropertyChanged(nameof(IsSearchPatternInvalid));
        }

        private void SyncSearchToCoordinator(bool apply)
        {
            if (_suppressCoordinatorSync)
                return;
            if (apply)
                _filter.SetSearch(SearchText, IsMatchCase, IsMatchWholeWord, UseRegularExpressions, IsMatchLogLevel, IsSearchProcess);
            else
                // Regex validity/icon only; Apply would rebuild the list on every character.
                _filter.UpdateSearchFields(SearchText, IsMatchCase, IsMatchWholeWord, UseRegularExpressions, IsMatchLogLevel, IsSearchProcess);
        }

        private void RefreshFindPreviousEnabled()
        {
            IsEnableFindPrevious = !string.IsNullOrEmpty(_searchText) && _state.SelectedLog != null;
        }

        /// <summary>
        /// Snapshot of current Logs for Core navigation. Same collection instance and Count — reuse the cache.
        /// </summary>
        private List<LogEntry> GetViewEntries()
        {
            var logs = _state.Logs;
            if (_cachedView != null && ReferenceEquals(_cachedViewSource, logs) && _cachedViewCount == (logs?.Count ?? 0))
                return _cachedView;

            _cachedView = ToEntries(logs);
            _cachedViewSource = logs;
            _cachedViewCount = logs?.Count ?? 0;
            return _cachedView;
        }

        private static List<LogEntry> ToEntries(IList<LogMessage> logs)
        {
            var list = new List<LogEntry>(logs?.Count ?? 0);
            if (logs == null)
                return list;
            for (int i = 0; i < logs.Count; i++)
                list.Add(LogEntryConverter.ToLogEntry(logs[i]));
            return list;
        }
    }
}
