using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LogViewer.Adapters;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.Services;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Repeats window. Counts the visible <see cref="LogViewState.Logs"/> by default; a toggle uses the whole buffer.
    /// Rebuild is debounced 1.5s like the timeline so a UDP storm does not aggregate on every row.
    /// Does not change the main list filter.
    /// </summary>
    public sealed class MessageGroupsViewModel : BaseViewModel, IResettable
    {
        private const int ProgressThreshold = 200000;

        private readonly LogViewState _state;
        private readonly IDialogService _dialogs;
        private readonly IUiTimer _timer;
        private bool _useFilteredList = true;
        private bool _windowOpen;
        private bool _dirty;
        private bool _showProgress;
        private string _filterText = string.Empty;
        private string _summaryText;
        private int _refreshId;
        private MessageGroupItem _selectedItem;
        private List<MessageGroupItem> _builtItems = new List<MessageGroupItem>();
        private RelayCommand _openCommand;
        private RelayCommand _refreshCommand;
        private RelayCommand _showLastCommand;
        private RelayCommand _showFirstCommand;

        public MessageGroupsViewModel(LogViewState state, IDialogService dialogs, IUiTimer timer)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _timer.Interval = TimeSpan.FromMilliseconds(1500);
            _timer.Tick += OnTimerTick;
            _state.LogsChanged += (sender, args) =>
            {
                // The filter already rebuilt Logs — compute immediately, do not wait for the UDP timer.
                if (_windowOpen && _useFilteredList)
                    Refresh();
            };
            _state.AllLogsChanged += (sender, args) =>
            {
                if (_windowOpen && !_useFilteredList)
                    Refresh();
            };
            ApplyItems(new List<MessageGroupItem>());
        }

        public event EventHandler<LogMessage> ShowLogEvent;

        public ObservableCollection<MessageGroupItem> Items { get; } = new ObservableCollection<MessageGroupItem>();

        public MessageGroupItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Default: the visible list, so groups respect the main window AND filter.</summary>
        public bool UseFilteredList
        {
            get => _useFilteredList;
            set
            {
                if (_useFilteredList == value)
                    return;
                _useFilteredList = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseAllLogs));
                if (_windowOpen)
                    Refresh();
            }
        }

        public bool UseAllLogs
        {
            get => !_useFilteredList;
            set
            {
                if (value)
                    UseFilteredList = false;
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        public bool ShowProgress
        {
            get => _showProgress;
            private set
            {
                _showProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowEmptyPlaceholder));
            }
        }

        public bool ShowEmptyPlaceholder => !_showProgress && Items.Count == 0;

        public string SummaryText
        {
            get => _summaryText;
            private set
            {
                _summaryText = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand OpenCommand => _openCommand ?? (_openCommand = new RelayCommand(Open));
        public RelayCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new RelayCommand(Refresh));
        public RelayCommand ShowLastCommand => _showLastCommand ?? (_showLastCommand = new RelayCommand(ShowLast));
        public RelayCommand ShowFirstCommand => _showFirstCommand ?? (_showFirstCommand = new RelayCommand(ShowFirst));

        /// <summary>UDP batch: mark dirty only. No aggregation while the window is closed.</summary>
        public void ScheduleRebuild()
        {
            if (!_windowOpen)
                return;
            _dirty = true;
            if (!_timer.IsEnabled)
                _timer.Start();
        }

        public void Refresh()
        {
            var _ = RefreshAsync();
        }

        /// <summary>Same as Refresh, awaitable for tests. Empty snapshot stays on the caller thread (no crash, no Task.Run).</summary>
        public async Task RefreshAsync()
        {
            if (!_windowOpen)
                return;

            _dirty = false;
            if (_timer.IsEnabled)
                _timer.Stop();

            var id = ++_refreshId;
            var snapshot = CaptureSource();
            ShowProgress = snapshot.Count > ProgressThreshold;
            try
            {
                List<MessageGroupItem> built;
                if (snapshot.Count == 0)
                    built = new List<MessageGroupItem>();
                else
                    built = await Task.Run(() => BuildItems(snapshot)).ConfigureAwait(true);
                if (id != _refreshId)
                    return;
                _builtItems = built;
                ApplyFilter();
            }
            finally
            {
                if (id == _refreshId)
                    ShowProgress = false;
            }
        }

        public void ShowItemLast(MessageGroupItem item)
        {
            if (item?.LastMessage == null)
                return;
            ShowLogEvent?.Invoke(this, item.LastMessage);
        }

        public void OnWindowClosed()
        {
            _windowOpen = false;
            _dirty = false;
            if (_timer.IsEnabled)
                _timer.Stop();
        }

        /// <inheritdoc />
        public void Reset()
        {
            _builtItems = new List<MessageGroupItem>();
            ApplyItems(new List<MessageGroupItem>());
            if (_windowOpen)
                Refresh();
        }

        public void DisposeTimer()
        {
            _timer.Stop();
            _timer.Dispose();
        }

        private void Open()
        {
            _windowOpen = true;
            Refresh();
            _dialogs.ShowOrActivateMessageGroups(this, message =>
            {
                if (message == null)
                    return;
                if (_state.Logs.Contains(message))
                {
                    _state.SelectedLog = message;
                    return;
                }

                // Whole-buffer toggle: the last hit may be hidden by the filter — find the same group in Logs.
                _state.SelectedLog = FindVisible(message) ?? message;
            });
        }

        private LogMessage FindVisible(LogMessage sample)
        {
            if (sample == null || _state.Logs == null)
                return null;
            var key = MessageFingerprint.BuildKey(
                (LogViewer.Core.Domain.LogLevel)(int)sample.Level,
                sample.Logger,
                sample.Message,
                sample.Throwable);
            LogMessage last = null;
            for (int i = 0; i < _state.Logs.Count; i++)
            {
                var row = _state.Logs[i];
                if (row == null)
                    continue;
                var rowKey = MessageFingerprint.BuildKey(
                    (LogViewer.Core.Domain.LogLevel)(int)row.Level,
                    row.Logger,
                    row.Message,
                    row.Throwable);
                if (rowKey == key)
                    last = row;
            }
            return last;
        }

        private IReadOnlyList<LogMessage> CaptureSource()
        {
            var source = _useFilteredList ? _state.Logs : _state.AllLogs;
            if (source == null || source.Count == 0)
                return Array.Empty<LogMessage>();
            return source.ToList();
        }

        private static List<MessageGroupItem> BuildItems(IReadOnlyList<LogMessage> snapshot)
        {
            var entries = new List<LogEntry>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var msg = snapshot[i];
                entries.Add(msg == null ? null : LogEntryConverter.ToLogEntry(msg));
            }

            var groups = MessageGroupAggregator.Build(entries);
            var items = new List<MessageGroupItem>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var first = IndexOrNull(snapshot, g.FirstIndex);
                var last = IndexOrNull(snapshot, g.LastIndex);
                items.Add(new MessageGroupItem(g, first, last));
            }
            return items;
        }

        private static LogMessage IndexOrNull(IReadOnlyList<LogMessage> snapshot, int index)
        {
            if (index < 0 || index >= snapshot.Count)
                return null;
            return snapshot[index];
        }

        private void ApplyFilter()
        {
            var filter = _filterText.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                ApplyItems(_builtItems);
                return;
            }

            var filtered = new List<MessageGroupItem>();
            for (int i = 0; i < _builtItems.Count; i++)
            {
                var item = _builtItems[i];
                if (Matches(item, filter))
                    filtered.Add(item);
            }
            ApplyItems(filtered);
        }

        private static bool Matches(MessageGroupItem item, string filter)
        {
            if (item == null)
                return false;
            return Contains(item.DisplayText, filter)
                   || Contains(item.Logger, filter)
                   || Contains(item.SampleMessage, filter)
                   || Contains(item.Headline, filter);
        }

        private static bool Contains(string text, string filter)
        {
            return !string.IsNullOrEmpty(text)
                   && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ApplyItems(List<MessageGroupItem> items)
        {
            Items.Clear();
            int occurrences = 0;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    Items.Add(items[i]);
                    occurrences += items[i].Count;
                }
            }

            SelectedItem = null;
            SummaryText = string.Format(Locals.MessageGroupsSummary, Items.Count, occurrences);
            OnPropertyChanged(nameof(ShowEmptyPlaceholder));
        }

        private void ShowLast() => ShowItemLast(SelectedItem);

        private void ShowFirst()
        {
            if (SelectedItem?.FirstMessage == null)
                return;
            ShowLogEvent?.Invoke(this, SelectedItem.FirstMessage);
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _timer.Stop();
            if (_dirty)
                Refresh();
        }
    }
}
