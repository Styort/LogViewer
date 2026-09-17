using System;
using System.Collections.Generic;
using LogViewer.Enums;
using LogViewer.Localization;
using LogViewer.MVVM.Models;

namespace LogViewer.Helpers
{
    /// <summary>
    /// Builds Warn/Error/Fatal density buckets from the currently displayed log list.
    /// The X axis uses absolute <see cref="LogMessage.Time"/> of <c>Logs</c>, not ListView row order,
    /// so sorting the Time column does not change the timeline.
    /// </summary>
    public static class ErrorTimelineBuilder
    {
        public const double BarHeight = 28;

        public static IList<ErrorTimelineBucket> Build(IEnumerable<LogMessage> logs, int bucketCount)
        {
            var empty = new List<ErrorTimelineBucket>();
            if (logs == null || bucketCount <= 0)
                return empty;

            DateTime minTime = DateTime.MaxValue;
            DateTime maxTime = DateTime.MinValue;
            var snapshot = logs as IList<LogMessage>;
            IEnumerable<LogMessage> source = snapshot ?? logs;
            int counted = 0;

            foreach (var log in source)
            {
                if (log == null || log.Time == default)
                    continue;
                counted++;
                if (log.Time < minTime)
                    minTime = log.Time;
                if (log.Time > maxTime)
                    maxTime = log.Time;
            }

            if (counted == 0)
                return empty;

            if (minTime == maxTime)
            {
                var single = CreateBucket(minTime, maxTime);
                FillSingleBucket(source, single);
                ApplyHeights(new[] { single });
                FormatToolTips(new[] { single });
                return new List<ErrorTimelineBucket> { single };
            }

            int count = bucketCount;
            var buckets = new ErrorTimelineBucket[count];
            long minTicks = minTime.Ticks;
            long maxTicks = maxTime.Ticks;
            long range = maxTicks - minTicks;

            for (int i = 0; i < count; i++)
            {
                var from = new DateTime(minTicks + range * i / count);
                var to = i == count - 1
                    ? maxTime
                    : new DateTime(minTicks + range * (i + 1) / count);
                buckets[i] = CreateBucket(from, to);
            }

            var firstAny = new LogMessage[count];

            foreach (var log in source)
            {
                if (log == null || log.Time == default)
                    continue;

                long ticks = log.Time.Ticks;
                if (ticks < minTicks || ticks > maxTicks)
                    continue;

                int index = (int)((ticks - minTicks) * (long)count / range);
                if (index >= count)
                    index = count - 1;
                if (index < 0)
                    continue;

                var bucket = buckets[index];
                if (IsWarnPlus(log.Level))
                {
                    if (bucket.FirstHit == null)
                        bucket.FirstHit = log;
                    CountLevel(log.Level, bucket);
                }
                else if (firstAny[index] == null)
                {
                    firstAny[index] = log;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (buckets[i].FirstHit == null)
                    buckets[i].FirstHit = firstAny[i];
            }

            ApplyHeights(buckets);
            FormatToolTips(buckets);
            return buckets;
        }

        private static ErrorTimelineBucket CreateBucket(DateTime from, DateTime to)
        {
            return new ErrorTimelineBucket
            {
                From = from,
                To = to
            };
        }

        private static void FillSingleBucket(IEnumerable<LogMessage> source, ErrorTimelineBucket bucket)
        {
            LogMessage firstAny = null;
            foreach (var log in source)
            {
                if (log == null || log.Time == default)
                    continue;
                if (IsWarnPlus(log.Level))
                {
                    if (bucket.FirstHit == null)
                        bucket.FirstHit = log;
                    CountLevel(log.Level, bucket);
                }
                else if (firstAny == null)
                {
                    firstAny = log;
                }
            }

            if (bucket.FirstHit == null)
                bucket.FirstHit = firstAny;
        }

        private static bool IsWarnPlus(eLogLevel level)
        {
            return level == eLogLevel.Warn || level == eLogLevel.Error || level == eLogLevel.Fatal;
        }

        private static void CountLevel(eLogLevel level, ErrorTimelineBucket bucket)
        {
            if (level == eLogLevel.Warn)
                bucket.Warn++;
            else if (level == eLogLevel.Error)
                bucket.Error++;
            else if (level == eLogLevel.Fatal)
                bucket.Fatal++;
        }

        private static void ApplyHeights(IList<ErrorTimelineBucket> buckets)
        {
            int maxTotal = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                int total = buckets[i].Warn + buckets[i].Error + buckets[i].Fatal;
                if (total > maxTotal)
                    maxTotal = total;
            }

            if (maxTotal <= 0)
                return;

            double scale = BarHeight / maxTotal;
            for (int i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                if (bucket.Warn > 0)
                    bucket.WarnHeight = Math.Max(2, bucket.Warn * scale);
                if (bucket.Error > 0)
                    bucket.ErrorHeight = Math.Max(2, bucket.Error * scale);
                if (bucket.Fatal > 0)
                    bucket.FatalHeight = Math.Max(2, bucket.Fatal * scale);

                double stacked = bucket.WarnHeight + bucket.ErrorHeight + bucket.FatalHeight;
                if (stacked > BarHeight && stacked > 0)
                {
                    double fit = BarHeight / stacked;
                    bucket.WarnHeight *= fit;
                    bucket.ErrorHeight *= fit;
                    bucket.FatalHeight *= fit;
                }
            }
        }

        private static void FormatToolTips(IList<ErrorTimelineBucket> buckets)
        {
            var format = Settings.Instance?.DataFormat;
            if (string.IsNullOrEmpty(format))
                format = "dd/MM/yyyy HH:mm:ss.fff";

            for (int i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                bucket.ToolTip = string.Format(
                    Locals.ErrorTimelineToolTip,
                    bucket.From.ToString(format),
                    bucket.To.ToString(format),
                    bucket.Warn,
                    bucket.Error,
                    bucket.Fatal);
            }
        }
    }
}
