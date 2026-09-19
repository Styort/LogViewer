using System;
using System.Windows.Threading;

namespace LogViewer.Services.Wpf
{
    /// <summary>DispatcherTimer on the UI thread; Dispose only Stop — the app dispatcher stays alive.</summary>
    public sealed class DispatcherUiTimer : IUiTimer
    {
        private readonly DispatcherTimer _timer;

        public DispatcherUiTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += (sender, args) => Tick?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        /// <inheritdoc />
        public bool IsEnabled => _timer.IsEnabled;

        /// <inheritdoc />
        public event EventHandler Tick;

        /// <inheritdoc />
        public void Start() => _timer.Start();

        /// <inheritdoc />
        public void Stop() => _timer.Stop();

        /// <summary>Stop only. The app Dispatcher must keep running.</summary>
        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
