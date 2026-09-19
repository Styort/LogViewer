using System;
using System.Collections.Generic;
using System.Threading;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Coalesces per-entry notifications so a UDP burst does not enqueue one UI marshal per packet.
    /// The first queued item arms a short timer; reaching <see cref="DefaultFlushThreshold"/> flushes immediately.
    /// </summary>
    /// <remarks>
    /// Lives in Core so flush order can be tested without WPF. The adapter still Posts the snapshot.
    /// Invariant: callers must <see cref="Flush"/> before trim/filter-refresh events so pending adds apply first,
    /// and <see cref="Discard"/> on session clear so dropped entries never appear.
    /// Flush invokes the callback outside the queue lock and does not marshal threads.
    /// </remarks>
    public sealed class LogEntryBatcher : IDisposable
    {
        /// <summary>
        /// ~75 ms keeps the list feeling live without flooding the Dispatcher under hundreds of packets/sec.
        /// </summary>
        public const int DefaultFlushIntervalMs = 75;

        /// <summary>
        /// Cap so a storm still delivers in chunks of a few hundred Adds per marshal instead of waiting for the timer.
        /// </summary>
        public const int DefaultFlushThreshold = 300;

        private readonly object _sync = new object();
        private readonly List<LogEntryProcessedEventArgs> _queue = new List<LogEntryProcessedEventArgs>();
        private readonly Action<IReadOnlyList<LogEntryProcessedEventArgs>, int> _onFlush;
        private readonly int _flushIntervalMs;
        private readonly int _flushThreshold;
        private readonly Timer _timer;
        private bool _timerArmed;
        private int _epoch;
        private bool _disposed;

        public LogEntryBatcher(Action<IReadOnlyList<LogEntryProcessedEventArgs>, int> onFlush,
            int flushIntervalMs = DefaultFlushIntervalMs,
            int flushThreshold = DefaultFlushThreshold)
        {
            if (flushIntervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(flushIntervalMs));
            if (flushThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(flushThreshold));
            _onFlush = onFlush ?? throw new ArgumentNullException(nameof(onFlush));
            _flushIntervalMs = flushIntervalMs;
            _flushThreshold = flushThreshold;
            _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Returns true if <paramref name="epoch"/> still matches the batcher (false after <see cref="Discard"/>).
        /// Used so an already-posted UI callback can drop a batch that was invalidated by Clear.
        /// </summary>
        public bool IsCurrentEpoch(int epoch)
        {
            lock (_sync)
                return !_disposed && epoch == _epoch;
        }

        public void Enqueue(LogEntryProcessedEventArgs item)
        {
            if (item == null) return;
            bool flushNow = false;
            lock (_sync)
            {
                if (_disposed) return;
                _queue.Add(item);
                if (_queue.Count >= _flushThreshold)
                    flushNow = true;
                else
                    ArmTimerNoLock();
            }

            if (flushNow)
                Flush();
        }

        /// <summary>
        /// Delivers queued items in receive order via the flush callback. No-op if the queue is empty.
        /// </summary>
        public void Flush()
        {
            List<LogEntryProcessedEventArgs> batch;
            int epoch;
            lock (_sync)
            {
                if (_disposed || _queue.Count == 0)
                    return;
                batch = new List<LogEntryProcessedEventArgs>(_queue);
                _queue.Clear();
                epoch = _epoch;
                DisarmTimerNoLock();
            }

            _onFlush(batch, epoch);
        }

        /// <summary>
        /// Drops unsent items (session was cleared in Core) and invalidates in-flight flush callbacks.
        /// </summary>
        public void Discard()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _queue.Clear();
                _epoch++;
                DisarmTimerNoLock();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _queue.Clear();
                _epoch++;
                DisarmTimerNoLock();
            }

            _timer.Dispose();
        }

        private void OnTimer(object state)
        {
            Flush();
        }

        private void ArmTimerNoLock()
        {
            if (_timerArmed) return;
            _timerArmed = true;
            _timer.Change(_flushIntervalMs, Timeout.Infinite);
        }

        private void DisarmTimerNoLock()
        {
            if (!_timerArmed) return;
            _timerArmed = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}
