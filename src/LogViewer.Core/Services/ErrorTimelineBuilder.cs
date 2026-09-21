using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// One displayed row for bucket layout. <see cref="Index"/> is the caller's list index
    /// so the UI can resolve FirstHit without Core knowing WPF types.
    /// </summary>
    public struct TimelineEvent
    {
        /// <summary>Absolute timestamp; list order is ignored when placing the event on X.</summary>
        public DateTime Time;

        /// <summary>Warn/Error/Fatal feed the error stack; every level increments TotalCount.</summary>
        public LogLevel Level;

        /// <summary>Index in the caller's displayed list (not Core session index).</summary>
        public int Index;
    }

    /// <summary>Density counts for one time slice. No WPF types.</summary>
    public sealed class TimelineBucket
    {
        /// <summary>Inclusive start of the slice on the absolute time axis.</summary>
        public DateTime From { get; set; }

        /// <summary>Inclusive end of the slice.</summary>
        public DateTime To { get; set; }

        /// <summary>All levels in this slice. Rate height uses this, not Warn+Error+Fatal.</summary>
        public int TotalCount { get; set; }

        public int Warn { get; set; }
        public int Error { get; set; }
        public int Fatal { get; set; }

        /// <summary>
        /// Index of the first row in this slice (any level) in list order; -1 if the slice is empty.
        /// Click target for the overlay strip; error stack and rate share From/To.
        /// </summary>
        public int FirstHitIndex { get; set; } = -1;

        /// <summary>
        /// Approximate messages/sec for the tooltip. A zero-length slice (min==max) returns 0 so callers never divide by zero.
        /// </summary>
        public double MessagesPerSecond
        {
            get
            {
                double seconds = (To - From).TotalSeconds;
                if (seconds <= 0)
                    return 0;
                return TotalCount / seconds;
            }
        }
    }

    /// <summary>
    /// Time-slice buckets on absolute timestamps (not list order): TotalCount for the rate fill plus Warn/Error/Fatal.
    /// Both overlays share From/To so they stay aligned on X.
    /// </summary>
    /// <remarks>
    /// Sorting the Time column must not reshuffle the strip: X is min..max of <see cref="TimelineEvent.Time"/>.
    /// When min==max there is a single bucket so a one-event list still has a clickable FirstHit.
    /// </remarks>
    public static class ErrorTimelineBuilder
    {
        /// <summary>
        /// Layout <paramref name="events"/> into <paramref name="bucketCount"/> slices.
        /// Empty input or non-positive count returns an empty list (no dummy buckets).
        /// </summary>
        public static IList<TimelineBucket> Build(IReadOnlyList<TimelineEvent> events, int bucketCount)
        {
            var empty = new List<TimelineBucket>();
            if (events == null || events.Count == 0 || bucketCount <= 0)
                return empty;

            DateTime minTime = DateTime.MaxValue;
            DateTime maxTime = DateTime.MinValue;
            int counted = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Time == default(DateTime))
                    continue;
                counted++;
                if (ev.Time < minTime)
                    minTime = ev.Time;
                if (ev.Time > maxTime)
                    maxTime = ev.Time;
            }

            if (counted == 0)
                return empty;

            if (minTime == maxTime)
            {
                var single = new TimelineBucket { From = minTime, To = maxTime, FirstHitIndex = -1 };
                FillSingle(events, single);
                return new List<TimelineBucket> { single };
            }

            int count = bucketCount;
            var buckets = new TimelineBucket[count];
            long minTicks = minTime.Ticks;
            long maxTicks = maxTime.Ticks;
            long range = maxTicks - minTicks;
            var firstAny = new int[count];
            for (int i = 0; i < count; i++)
            {
                firstAny[i] = -1;
                var from = new DateTime(minTicks + range * i / count);
                var to = i == count - 1
                    ? maxTime
                    : new DateTime(minTicks + range * (i + 1) / count);
                buckets[i] = new TimelineBucket { From = from, To = to, FirstHitIndex = -1 };
            }

            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Time == default(DateTime))
                    continue;
                long ticks = ev.Time.Ticks;
                if (ticks < minTicks || ticks > maxTicks)
                    continue;

                int index = (int)((ticks - minTicks) * (long)count / range);
                if (index >= count)
                    index = count - 1;
                if (index < 0)
                    continue;

                var bucket = buckets[index];
                bucket.TotalCount++;
                if (IsWarnPlus(ev.Level))
                    CountLevel(ev.Level, bucket);
                if (firstAny[index] < 0)
                    firstAny[index] = ev.Index;
            }

            for (int i = 0; i < count; i++)
                buckets[i].FirstHitIndex = firstAny[i];

            return buckets;
        }

        private static void FillSingle(IReadOnlyList<TimelineEvent> events, TimelineBucket bucket)
        {
            int firstAny = -1;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Time == default(DateTime))
                    continue;
                bucket.TotalCount++;
                if (IsWarnPlus(ev.Level))
                    CountLevel(ev.Level, bucket);
                if (firstAny < 0)
                    firstAny = ev.Index;
            }

            bucket.FirstHitIndex = firstAny;
        }

        private static bool IsWarnPlus(LogLevel level)
        {
            return level == LogLevel.Warn || level == LogLevel.Error || level == LogLevel.Fatal;
        }

        private static void CountLevel(LogLevel level, TimelineBucket bucket)
        {
            if (level == LogLevel.Warn)
                bucket.Warn++;
            else if (level == LogLevel.Error)
                bucket.Error++;
            else if (level == LogLevel.Fatal)
                bucket.Fatal++;
        }
    }
}
