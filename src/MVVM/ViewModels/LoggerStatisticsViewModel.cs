using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;

namespace LogViewer.MVVM.ViewModels
{
    public class LoggerStatisticsViewModel : BaseViewModel
    {
        private const int ProgressThreshold = 200000;
        private const double LevelBarMaxWidth = 360;

        private readonly Func<bool, IReadOnlyList<LogMessage>> getSource;
        private bool useFilteredList = true;
        private bool showProgress;
        private int refreshId;
        private LoggerStatItem selectedItem;
        private string totalMessagesText;
        private string loggerCountText;
        private string errorFatalText;
        private string timeRangeText;
        private double traceBarWidth;
        private double debugBarWidth;
        private double infoBarWidth;
        private double warnBarWidth;
        private double errorBarWidth;
        private double fatalBarWidth;
        private RelayCommand refreshCommand;
        private RelayCommand showSelectedCommand;

        public LoggerStatisticsViewModel(Func<bool, IReadOnlyList<LogMessage>> getSource)
        {
            this.getSource = getSource ?? throw new ArgumentNullException(nameof(getSource));
            ApplyResult(new LoggerStatisticsResult());
        }

        public event EventHandler<LogMessage> ShowLogEvent;

        public ObservableCollection<LoggerStatItem> Items { get; } = new ObservableCollection<LoggerStatItem>();

        public LoggerStatItem SelectedItem
        {
            get => selectedItem;
            set
            {
                selectedItem = value;
                OnPropertyChanged();
            }
        }

        public bool UseFilteredList
        {
            get => useFilteredList;
            set
            {
                if (useFilteredList == value)
                    return;
                useFilteredList = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseAllLogs));
                Refresh();
            }
        }

        public bool UseAllLogs
        {
            get => !useFilteredList;
            set
            {
                if (value)
                    UseFilteredList = false;
            }
        }

        public bool ShowProgress
        {
            get => showProgress;
            private set
            {
                showProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowEmptyPlaceholder));
            }
        }

        public bool ShowEmptyPlaceholder => !showProgress && Items.Count == 0;

        public string TotalMessagesText
        {
            get => totalMessagesText;
            private set
            {
                totalMessagesText = value;
                OnPropertyChanged();
            }
        }

        public string LoggerCountText
        {
            get => loggerCountText;
            private set
            {
                loggerCountText = value;
                OnPropertyChanged();
            }
        }

        public string ErrorFatalText
        {
            get => errorFatalText;
            private set
            {
                errorFatalText = value;
                OnPropertyChanged();
            }
        }

        public string TimeRangeText
        {
            get => timeRangeText;
            private set
            {
                timeRangeText = value;
                OnPropertyChanged();
            }
        }

        public double TraceBarWidth
        {
            get => traceBarWidth;
            private set
            {
                traceBarWidth = value;
                OnPropertyChanged();
            }
        }

        public double DebugBarWidth
        {
            get => debugBarWidth;
            private set
            {
                debugBarWidth = value;
                OnPropertyChanged();
            }
        }

        public double InfoBarWidth
        {
            get => infoBarWidth;
            private set
            {
                infoBarWidth = value;
                OnPropertyChanged();
            }
        }

        public double WarnBarWidth
        {
            get => warnBarWidth;
            private set
            {
                warnBarWidth = value;
                OnPropertyChanged();
            }
        }

        public double ErrorBarWidth
        {
            get => errorBarWidth;
            private set
            {
                errorBarWidth = value;
                OnPropertyChanged();
            }
        }

        public double FatalBarWidth
        {
            get => fatalBarWidth;
            private set
            {
                fatalBarWidth = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand RefreshCommand => refreshCommand ?? (refreshCommand = new RelayCommand(Refresh));
        public RelayCommand ShowSelectedCommand => showSelectedCommand ?? (showSelectedCommand = new RelayCommand(ShowSelected));

        public async void Refresh()
        {
            var id = ++refreshId;
            IReadOnlyList<LogMessage> snapshot;
            var filtered = useFilteredList;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                snapshot = dispatcher.Invoke(() => getSource(filtered) ?? Array.Empty<LogMessage>());
            else
                snapshot = getSource(filtered) ?? Array.Empty<LogMessage>();

            ShowProgress = snapshot.Count > ProgressThreshold;
            try
            {
                var result = await Task.Run(() => LoggerStatistics.Build(snapshot)).ConfigureAwait(true);
                if (id != refreshId)
                    return;
                ApplyResult(result);
            }
            finally
            {
                if (id == refreshId)
                    ShowProgress = false;
            }
        }

        public void ShowItem(LoggerStatItem item)
        {
            if (item?.LastMessage == null)
                return;
            ShowLogEvent?.Invoke(this, item.LastMessage);
        }

        private void ShowSelected()
        {
            ShowItem(SelectedItem);
        }

        private void ApplyResult(LoggerStatisticsResult result)
        {
            var maxTotal = 0;
            if (result.Items != null)
            {
                foreach (var item in result.Items)
                {
                    if (item.Total > maxTotal)
                        maxTotal = item.Total;
                }
            }

            var barBrush = Settings.Instance?.CurrentTheme?.Color ?? new SolidColorBrush(Color.FromRgb(0x3F, 0x51, 0xB5));
            if (barBrush.CanFreeze)
                barBrush.Freeze();

            Items.Clear();
            if (result.Items != null)
            {
                foreach (var stat in result.Items)
                    Items.Add(new LoggerStatItem(stat, maxTotal, barBrush));
            }

            SelectedItem = null;
            TotalMessagesText = $"{Locals.StatisticsTotal}: {result.TotalMessages}";
            LoggerCountText = $"{Locals.StatisticsLoggers}: {result.LoggerCount}";
            ErrorFatalText = $"{Locals.StatisticsErrorFatal}: {result.ErrorFatalCount}";
            TimeRangeText = result.MinTime.HasValue && result.MaxTime.HasValue
                ? $"{Locals.StatisticsTimeRange}: {result.MinTime:dd/MM/yyyy HH:mm:ss} – {result.MaxTime:dd/MM/yyyy HH:mm:ss}"
                : $"{Locals.StatisticsTimeRange}: —";

            var levelTotal = result.Trace + result.Debug + result.Info + result.Warn + result.Error + result.Fatal;
            TraceBarWidth = LevelWidth(result.Trace, levelTotal);
            DebugBarWidth = LevelWidth(result.Debug, levelTotal);
            InfoBarWidth = LevelWidth(result.Info, levelTotal);
            WarnBarWidth = LevelWidth(result.Warn, levelTotal);
            ErrorBarWidth = LevelWidth(result.Error, levelTotal);
            FatalBarWidth = LevelWidth(result.Fatal, levelTotal);

            OnPropertyChanged(nameof(ShowEmptyPlaceholder));
        }

        private static double LevelWidth(int count, int total)
        {
            return total > 0 ? LevelBarMaxWidth * count / total : 0;
        }
    }
}
