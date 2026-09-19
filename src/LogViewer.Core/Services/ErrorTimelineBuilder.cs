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

        /// <summary>Used only to count Warn/Error/Fatal; other levels can still supply FirstHit fallback.</summary>
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

        public int Warn { get; set; }
        public int Error { get; set; }
        public int Fatal { get; set; }

        /// <summary>
        /// Index of the first Warn/Error/Fatal in this slice, or a quiet row if none; -1 if the slice is empty.
        /// </summary>
        public int FirstHitIndex { get; set; } = -1;
    }

    /// <summary>
    /// Warn/Error/Fatal density buckets on absolute timestamps (not list order).
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
                if (IsWarnPlus(ev.Level))
                {
                    if (bucket.FirstHitIndex < 0)
                        bucket.FirstHitIndex = ev.Index;
                    CountLevel(ev.Level, bucket);
                }
                else if (firstAny[index] < 0)
                {
                    // Quiet rows are a fallback click target when the slice has no Warn/Error/Fatal.
                    firstAny[index] = ev.Index;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (buckets[i].FirstHitIndex < 0)
                    buckets[i].FirstHitIndex = firstAny[i];
            }

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
                if (IsWarnPlus(ev.Level))
                {
                    if (bucket.FirstHitIndex < 0)
                        bucket.FirstHitIndex = ev.Index;
                    CountLevel(ev.Level, bucket);
                }
                else if (firstAny < 0)
                {
                    firstAny = ev.Index;
                }
            }

            if (bucket.FirstHitIndex < 0)
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
