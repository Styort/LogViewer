using System;
using System.Collections.ObjectModel;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.Services;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Error density over a messages/sec fill. Rebuilding on every UDP batch is too expensive —
    /// <see cref="ScheduleRebuild"/> coalesces ticks for 1.5s.
    /// </summary>
    public sealed class ErrorTimelineViewModel : BaseViewModel, IResettable
    {
        private readonly LogViewState _state;
        private readonly IAppSettings _settings;
        private readonly IUiTimer _timer;
        private ObservableCollection<ErrorTimelineBucket> _buckets = new ObservableCollection<ErrorTimelineBucket>();
        private bool _visible;
        private int _bucketCount = 120;
        private bool _dirty;
        private RelayCommand _jumpCommand;
        private RelayCommand _setCountCommand;

        public ErrorTimelineViewModel(LogViewState state, IAppSettings settings, IUiTimer timer)
        {
            _state = state;
            _settings = settings;
            _timer = timer;
            _timer.Interval = TimeSpan.FromMilliseconds(1500);
            _timer.Tick += OnTimerTick;
            _state.LogsChanged += (sender, args) => Rebuild();
        }

        /// <summary>Strip buckets (error stack + rate). Empty if the setting is off or there are no timestamps.</summary>
        public ObservableCollection<ErrorTimelineBucket> ErrorTimelineBuckets
        {
            get => _buckets;
            private set
            {
                _buckets = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Hide the whole strip if buckets are empty or the setting is off — do not leave empty padding.</summary>
        public bool IsErrorTimelineVisible
        {
            get => _visible;
            private set
            {
                if (_visible == value)
                    return;
                _visible = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Click a bucket: select FirstHit in the list.</summary>
        public RelayCommand JumpToTimelineBucketCommand =>
            _jumpCommand ?? (_jumpCommand = new RelayCommand(JumpToTimelineBucket));

        /// <summary>Bucket-count slider (40..200).</summary>
        public RelayCommand SetErrorTimelineBucketCountCommand =>
            _setCountCommand ?? (_setCountCommand = new RelayCommand(SetErrorTimelineBucketCount));

        /// <summary>Defer Rebuild by the timer Interval. Do not compute buckets on the hot UDP path.</summary>
        public void ScheduleRebuild()
        {
            _dirty = true;
            if (!_timer.IsEnabled)
                _timer.Start();
        }

        /// <summary>Rebuild immediately. LogsChanged comes here: the filter already changed the list, no debounce.</summary>
        public void Rebuild()
        {
            _dirty = false;
            if (_timer.IsEnabled)
                _timer.Stop();

            if (!_settings.IsShowErrorTimeline)
            {
                ErrorTimelineBuckets = new ObservableCollection<ErrorTimelineBucket>();
                IsErrorTimelineVisible = false;
                return;
            }

            var source = _state.Logs;
            if (source == null || source.Count == 0)
            {
                ErrorTimelineBuckets = new ObservableCollection<ErrorTimelineBucket>();
                IsErrorTimelineVisible = false;
                return;
            }

            var buckets = ErrorTimelineBuilder.Build(source, _bucketCount, _settings.DataFormat);
            ErrorTimelineBuckets = new ObservableCollection<ErrorTimelineBucket>(buckets);
            // Info-only traffic must still show the strip (rate fill, empty error stack).
            IsErrorTimelineVisible = buckets.Count > 0;
        }

        /// <inheritdoc />
        public void Reset()
        {
            Rebuild();
        }

        /// <summary>Host.Dispose: stop the timer, otherwise Tick fires after the window is closed.</summary>
        public void DisposeTimer()
        {
            _timer.Stop();
            _timer.Dispose();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _timer.Stop();
            if (_dirty)
                Rebuild();
        }

        private void JumpToTimelineBucket(object obj)
        {
            var bucket = obj as ErrorTimelineBucket;
            if (bucket?.FirstHit == null)
                return;
            _state.SelectedLog = bucket.FirstHit;
        }

        private void SetErrorTimelineBucketCount(object obj)
        {
            if (!(obj is int count))
                return;
            // Window slider: too few buckets make the strip useless; too many slow rendering.
            count = Math.Max(40, Math.Min(200, count));
            if (count == _bucketCount)
                return;
            _bucketCount = count;
            Rebuild();
        }
    }
}
