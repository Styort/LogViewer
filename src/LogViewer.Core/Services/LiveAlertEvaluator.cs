using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Pure "should this UI batch raise a live Error/Fatal alert" plus notify throttle.
    /// </summary>
    /// <remarks>
    /// Threshold is Error and Fatal only — Warn is not an alert (too noisy on a busy system).
    /// Compare enum equality, not <c>HasFlag</c>: <see cref="Domain.LogLevel"/> is a cumulative
    /// flags enum, so Warn includes the Error bits.
    /// Don't Receive never reaches this type: <see cref="LogProcessingService"/> drops those
    /// packets before <c>EntryProcessed</c>, so they are absent from the UI batch.
    /// Don't Show / current min level does not suppress the alert: the row is already in the
    /// session buffer, and the user would see it with min=Error. Alerting only on
    /// <c>IncludedInFilter</c> would hide Errors while the list is filtered to Fatal.
    /// Sound/balloon are throttled against the UI batch (task 02): one batch of 200 Errors is
    /// one decision, and a continuous stream is limited to once per
    /// <see cref="DefaultNotifyThrottle"/>. Auto-pause is not throttled here — the UI pauses
    /// once while receive is already running.
    /// </remarks>
    public sealed class LiveAlertEvaluator
    {
        /// <summary>
        /// 4 s: long enough to collapse a UDP storm into one sound/balloon, short enough
        /// that a later Error burst is still noticeable after the first pause.
        /// </summary>
        public static readonly TimeSpan DefaultNotifyThrottle = TimeSpan.FromSeconds(4);

        private DateTime _lastNotifyUtc = DateTime.MinValue;

        /// <summary>
        /// True if the batch contains at least one stored Error or Fatal (Don't Receive already excluded).
        /// Display-filter flag is ignored on purpose.
        /// </summary>
        public static bool BatchContainsAlertableEntry(IReadOnlyList<LogEntryProcessedEventArgs> batch)
        {
            if (batch == null || batch.Count == 0)
                return false;

            for (int i = 0; i < batch.Count; i++)
            {
                var entry = batch[i]?.Entry;
                if (entry == null)
                    continue;
                if (entry.Level == LogLevel.Error || entry.Level == LogLevel.Fatal)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Consumes one sound/balloon slot. Returns false when called again before <paramref name="throttle"/>.
        /// Does not move the timestamp on a suppressed call, so a storm cannot slide the window.
        /// </summary>
        public bool TryConsumeNotify(DateTime utcNow, TimeSpan throttle)
        {
            if (throttle < TimeSpan.Zero)
                throttle = TimeSpan.Zero;
            if (utcNow - _lastNotifyUtc < throttle)
                return false;
            _lastNotifyUtc = utcNow;
            return true;
        }
    }
}
