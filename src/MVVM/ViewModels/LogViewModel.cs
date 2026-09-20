using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using LogViewer.Adapters;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.Factories;
using LogViewer.Helpers;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;

namespace LogViewer.MVVM.ViewModels
{
    /// <summary>
    /// DataContext of the main window. New features (06+) are a child VM + Core logic; this host must not grow.
    /// See docs/architecture-log-view.md.
    /// </summary>
    public class LogViewModel : BaseViewModel, IDisposable
    {
        private readonly IAppSettings _settings;
        private readonly IDialogService _dialogs;
        private readonly IClipboardService _clipboard;
        private readonly LogViewState _state;
        private readonly FilterCoordinator _coordinator;
        private readonly LogSession _session;
        private readonly LogProcessingService _processing;
        private readonly CoreToUiAdapter _adapter;
        private readonly LogSessionPresenter _presenter;
        private readonly SettingsChangeApplier _settingsApplier = new SettingsChangeApplier();
        private readonly RowHighlightApplier _highlight;
        private readonly IResettable[] _parts;

        private bool _cleanIsEnabled;
        private bool _isShowTaskbarProgress;
        private eLogLevel _selectedMinLogLevel = eLogLevel.Trace;
        private bool _isSourceVisible;
        private bool _isThreadVisible;
        private SolidColorBrush _iconColor;
        private SolidColorBrush _fontColor = new SolidColorBrush(Colors.White);
        private string _messageFontFamily = "Consolas";
        private double _messageFontSize = 14;
        private RelayCommand _cleanCommand;
        private RelayCommand _openSettingsCommand;
        private RelayCommand _copyMessageCommand;
        private RelayCommand _copyLogCommand;
        private RelayCommand _openStatsCommand;

        /// <summary>
        /// For XAML / designer: the factory reads SynchronizationContext of the current thread.
        /// Tests use the constructor that takes <see cref="LogViewModelDependencies"/>.
        /// </summary>
        public LogViewModel() : this(LogViewModelFactory.CreateDependencies()) { }

        public LogViewModel(LogViewModelDependencies d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            _settings = d.Settings;
            _dialogs = d.Dialogs;
            _clipboard = d.Clipboard;
            _state = d.ViewState;
            _coordinator = d.Coordinator;
            _session = d.Session;
            _processing = d.Processing;
            _adapter = d.Adapter;
            _highlight = d.HighlightApplier;

            IconColor = _settings.CurrentTheme.Color;
            FontColor = FontColor.FromARGB(_settings.FontColor);
            ApplyMessageDisplaySettings();
            IsSourceVisible = _settings.IsShowSourceColumn;
            IsThreadVisible = _settings.IsShowThreadColumn;

            Receivers = new ReceiversViewModel(d.Processing, d.Adapter, d.Coordinator, d.SourceFactory, d.Dialogs, d.Settings.Receivers);
            Timeline = new ErrorTimelineViewModel(d.ViewState, d.Settings, d.TimelineTimer);
            Bookmarks = new BookmarksViewModel(d.ViewState, d.Dialogs);
            MessageGroups = new MessageGroupsViewModel(d.ViewState, d.Dialogs, d.MessageGroupsTimer);
            Import = new ImportViewModel(d.ViewState, d.Session, d.Processing, d.ImportService, d.Adapter, d.FileWatch, d.Dialogs, d.Files, Receivers, FormatLogLineForClipboard);
            Search = new SearchViewModel(d.ViewState, d.Coordinator, d.Session, d.Processing, d.Projector, d.Query, d.Dialogs, d.Settings, () => SelectedMinLogLevel);
            Tree = new LoggerTreeViewModel(d.ViewState, d.Coordinator, d.Session, d.Processing, d.FileWatch, d.TreeBuilder, d.TreeMarker, d.Settings, Receivers, Import, Clean);
            FilterPresets = new FilterPresetsViewModel(d.Coordinator, d.Dialogs, Search, Tree, SetMinLevelFromPreset);

            _parts = new IResettable[] { Receivers, Import, Tree, Search, Bookmarks, Timeline, MessageGroups };
            _presenter = new LogSessionPresenter(d.ViewState, d.Projector, Tree, Bookmarks, Timeline, MessageGroups, v => CleanIsEnabled = v);

            _state.PropertyChanged += (sender, e) => OnPropertyChanged(e.PropertyName);
            Receivers.PropertyChanged += (sender, e) => RefreshTaskbar();
            Import.PropertyChanged += (sender, e) =>
            {
                OnPropertyChanged(e.PropertyName);
                RefreshTaskbar();
            };

            _adapter.EntriesProcessed += _presenter.OnEntriesProcessed;
            _adapter.SessionCleared += _presenter.OnSessionCleared;
            _adapter.EntriesRemoved += _presenter.OnEntriesRemoved;
            _adapter.FilteredViewUpdated += _presenter.OnFilteredViewUpdated;
            _adapter.Subscribe();
            _coordinator.Apply();

            if (_settings.AutoStartInStartup && !App.IsManualStartup)
                Receivers.Start();
        }

        public ReceiversViewModel Receivers { get; }
        public ImportViewModel Import { get; }
        /// <summary>Logger tree. XAML binds to Tree.*, not to the host.</summary>
        public LoggerTreeViewModel Tree { get; }
        public SearchViewModel Search { get; }
        public BookmarksViewModel Bookmarks { get; }
        public ErrorTimelineViewModel Timeline { get; }
        public MessageGroupsViewModel MessageGroups { get; }
        public FilterPresetsViewModel FilterPresets { get; }

        /// <summary>Proxy for <see cref="LogViewState.Logs"/> — window DataContext was not changed, XAML still uses Logs.</summary>
        public AsyncObservableCollection<LogMessage> Logs { get => _state.Logs; set => _state.Logs = value; }
        public LogMessage SelectedLog { get => _state.SelectedLog; set => _state.SelectedLog = value; }
        public List<LogMessage> SelectedLogs { get => _state.SelectedLogs; set => _state.SelectedLogs = value; }
        public bool IsVisibleLoader { get => _state.IsBusy; set => _state.IsBusy = value; }
        public bool IsEnableLogList { get => _state.IsEnableLogList; set => _state.IsEnableLogList = value; }

        public bool CleanIsEnabled
        {
            get => _cleanIsEnabled;
            set { _cleanIsEnabled = value; OnPropertyChanged(); }
        }

        public bool IsShowTaskbarProgress
        {
            get => _isShowTaskbarProgress;
            set { _isShowTaskbarProgress = value && _settings.IsShowTaskbarProgress; OnPropertyChanged(); }
        }

        public IEnumerable<eLogLevel> LogLevels => Enum.GetValues(typeof(eLogLevel)).Cast<eLogLevel>().OrderByDescending(x => x);

        public eLogLevel SelectedMinLogLevel
        {
            get => _selectedMinLogLevel;
            set { _selectedMinLogLevel = value; _coordinator.SetMinLevel(value); OnPropertyChanged(); }
        }

        /// <summary>Preset Apply already wrote min level into the coordinator — update the combo only.</summary>
        private void SetMinLevelFromPreset(eLogLevel level)
        {
            _selectedMinLogLevel = level;
            OnPropertyChanged(nameof(SelectedMinLogLevel));
        }

        public bool IsSourceVisible
        {
            get => _isSourceVisible;
            set { _isSourceVisible = value; OnPropertyChanged(nameof(SourceColumnWidth)); OnPropertyChanged(); }
        }

        public bool IsThreadVisible
        {
            get => _isThreadVisible;
            set { _isThreadVisible = value; OnPropertyChanged(nameof(ThreadColumnWidth)); OnPropertyChanged(); }
        }

        public double SourceColumnWidth => IsSourceVisible ? 115 : 0;
        public double ThreadColumnWidth => IsThreadVisible ? double.NaN : 0;

        public SolidColorBrush IconColor { get => _iconColor; set { _iconColor = value; OnPropertyChanged(); } }
        public SolidColorBrush FontColor { get => _fontColor; set { _fontColor = value; OnPropertyChanged(); } }
        public string MessageFontFamily { get => _messageFontFamily; set { _messageFontFamily = value; OnPropertyChanged(); } }
        public double MessageFontSize { get => _messageFontSize; set { _messageFontSize = value; OnPropertyChanged(); } }

        public RelayCommand CleanCommand => _cleanCommand ?? (_cleanCommand = new RelayCommand(Clean));
        public RelayCommand OpenSettingsCommand => _openSettingsCommand ?? (_openSettingsCommand = new RelayCommand(OpenSettings));
        public RelayCommand CopyMessageCommand => _copyMessageCommand ?? (_copyMessageCommand = new RelayCommand(CopyMessage));
        public RelayCommand CopyLogCommand => _copyLogCommand ?? (_copyLogCommand = new RelayCommand(CopyLogs));
        public RelayCommand OpenLoggerStatisticsCommand => _openStatsCommand ?? (_openStatsCommand = new RelayCommand(OpenLoggerStatistics));

        public void ImportLogs(object obj) => Import.ImportLogs(obj);

        internal IReadOnlyList<LogMessage> GetStatisticsSource(bool filtered)
        {
            var source = filtered ? _state.Logs : _state.AllLogs;
            if (source == null || source.Count == 0)
                return Array.Empty<LogMessage>();
            return source.ToList();
        }

        private void Clean()
        {
            if (_state.IsBusy) return;
            _processing.ClearSession();
            if (_state.Logs.Any()) _state.Logs.Clear();
            foreach (var part in _parts)
                part.Reset();
            // Forced GC after a large import: otherwise the LOH keeps gigabytes until the next generation.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            CleanIsEnabled = false;
        }

        private void ApplyMessageDisplaySettings()
        {
            MessageFontFamily = _settings.MessageFontFamily;
            MessageFontSize = _settings.MessageFontSize;
        }

        private void OpenSettings()
        {
            var isProgress = !Receivers.StartIsEnabled;
            // While the settings window is open, sockets are stopped: changing the port on a live receive causes PortIsBusy.
            Receivers.Pause();
            if (_dialogs.ShowSettings() == true)
            {
                _settingsApplier.Apply(_settings, _state, _session, Receivers, Timeline,
                    v => IsSourceVisible = v, v => IsThreadVisible = v, ApplyMessageDisplaySettings,
                    b => IconColor = b, b => FontColor = b, IconColor, _highlight);
                if (isProgress) Receivers.Start();
            }
            else if (isProgress) Receivers.Start();
        }

        private void CopyMessage()
        {
            if (_state.SelectedLog == null) return;
            _clipboard.SetText(LogExportText.JoinMessageAndThrowable(_state.SelectedLog.Message, _state.SelectedLog.Throwable));
        }

        private void CopyLogs()
        {
            var logsToCopy = _state.GetLogsToCopy();
            if (logsToCopy.Count == 0) return;
            _clipboard.SetText(string.Join("\r\n", logsToCopy.Select(FormatLogLineForClipboard)));
        }

        private static string FormatLogLineForClipboard(LogMessage logMessage)
        {
            string pid = logMessage.ProcessID.HasValue ? $"{logMessage.ProcessID.Value};" : string.Empty;
            return $"{logMessage.Time:yy-MM-dd HH:mm:ss.ffff};{logMessage.Level};{pid}{logMessage.Thread};{logMessage.Logger};{LogExportText.JoinMessageAndThrowable(logMessage.Message, logMessage.Throwable)}";
        }

        private void OpenLoggerStatistics()
        {
            var vm = new LoggerStatisticsViewModel(GetStatisticsSource);
            _dialogs.ShowOrActivateLoggerStatistics(vm, message =>
            {
                if (message == null) return;
                if (_state.Logs.Contains(message)) { _state.SelectedLog = message; return; }
            // Statistics may hold a row from AllLogs; look up the same FullPath in the filtered list.
                _state.SelectedLog = _state.Logs.LastOrDefault(x => x.FullPath == message.FullPath) ?? message;
            });
        }

        private void RefreshTaskbar()
        {
            IsShowTaskbarProgress = !Receivers.StartIsEnabled || !Import.StartReadFromFileIsEnabled;
        }

        public void Dispose()
        {
            Timeline.DisposeTimer();
            MessageGroups.DisposeTimer();
            Import.Watch.RemoveAll();
            _adapter?.FlushPending();
            _adapter?.Dispose();
            _processing?.RemoveAllSources();
        }
    }
}
