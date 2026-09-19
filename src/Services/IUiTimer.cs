using System;

namespace LogViewer.Services
{
    /// <summary>
    /// UI-thread timer (error-timeline debounce).
    /// <c>DispatcherTimer</c> cannot be created in tests without a dispatcher, so VMs depend on this interface.
    /// </summary>
    public interface IUiTimer : IDisposable
    {
        /// <summary>Delay between ticks. The timeline uses 1.5s so buckets are not rebuilt on every UDP batch.</summary>
        TimeSpan Interval { get; set; }

        /// <summary>True while a deferred tick is pending; a second Start is not needed.</summary>
        bool IsEnabled { get; }

        /// <summary>Raised on the UI thread. The handler should Stop, otherwise the timer keeps running.</summary>
        event EventHandler Tick;

        /// <summary>Start counting Interval. A second call while already IsEnabled does not reset DispatcherTimer.</summary>
        void Start();

        /// <summary>Cancel a pending tick (the filter rebuilt the list before the timer fired).</summary>
        void Stop();
    }
}
