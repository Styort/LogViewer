using System;
using System.Collections.Generic;
using LogViewer.Enums;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using CoreTimeline = LogViewer.Core.Services.ErrorTimelineBuilder;
using CoreEvent = LogViewer.Core.Services.TimelineEvent;
using CoreLevel = LogViewer.Core.Domain.LogLevel;

namespace LogViewer.Helpers
{
    /// <summary>
    /// Maps UI log rows onto Core timeline buckets and applies bar heights / tooltips.
    /// </summary>
    public static class ErrorTimelineBuilder
    {
        /// <summary>
        /// Pixel cap for both overlays. Error stack and rate share the cap but not the scale:
        /// each uses its own max so Info volume does not flatten Warn/Error/Fatal.
        /// </summary>
        public const double BarHeight = 28;

        /// <summary>
        /// UI buckets with heights and localized tooltips. <paramref name="dateFormat"/> from settings.
        /// </summary>
        public static IList<ErrorTimelineBucket> Build(IEnumerable<LogMessage> logs, int bucketCount, string dateFormat = null)
        {
            var empty = new List<ErrorTimelineBucket>();
            if (logs == null || bucketCount <= 0)
                return empty;

            var snapshot = logs as IList<LogMessage> ?? new List<LogMessage>(logs);
            var events = new List<CoreEvent>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var log = snapshot[i];
                if (log == null)
                    continue;
                events.Add(new CoreEvent
                {
                    Time = log.Time,
                    Level = (CoreLevel)(int)log.Level,
                    Index = i
                });
            }

            var coreBuckets = CoreTimeline.Build(events, bucketCount);
            if (coreBuckets == null || coreBuckets.Count == 0)
                return empty;

            var result = new List<ErrorTimelineBucket>(coreBuckets.Count);
            for (int i = 0; i < coreBuckets.Count; i++)
            {
                var core = coreBuckets[i];
                var bucket = new ErrorTimelineBucket
                {
                    From = core.From,
                    To = core.To,
                    TotalCount = core.TotalCount,
                    Warn = core.Warn,
                    Error = core.Error,
                    Fatal = core.Fatal
                };
                if (core.FirstHitIndex >= 0 && core.FirstHitIndex < snapshot.Count)
                    bucket.FirstHit = snapshot[core.FirstHitIndex];
                result.Add(bucket);
            }

            ApplyHeights(result);
            FormatToolTips(result, dateFormat);
            return result;
        }

        private static void ApplyHeights(IList<ErrorTimelineBucket> buckets)
        {
            int maxErrors = 0;
            int maxTotal = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                int errors = buckets[i].Warn + buckets[i].Error + buckets[i].Fatal;
                if (errors > maxErrors)
                    maxErrors = errors;
                if (buckets[i].TotalCount > maxTotal)
                    maxTotal = buckets[i].TotalCount;
            }

            // Two maxima, one strip: Info flood must not flatten errors, and an Error burst
            // must not hide volume. Same From/To so the overlays stay aligned on X.
            double errorScale = maxErrors > 0 ? BarHeight / maxErrors : 0;
            double rateScale = maxTotal > 0 ? BarHeight / maxTotal : 0;

            for (int i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                // Floor 2px: a single Warn in a busy window would otherwise be a 0-height bar.
                if (bucket.Warn > 0)
                    bucket.WarnHeight = Math.Max(2, bucket.Warn * errorScale);
                if (bucket.Error > 0)
                    bucket.ErrorHeight = Math.Max(2, bucket.Error * errorScale);
                if (bucket.Fatal > 0)
                    bucket.FatalHeight = Math.Max(2, bucket.Fatal * errorScale);

                double stacked = bucket.WarnHeight + bucket.ErrorHeight + bucket.FatalHeight;
                if (stacked > BarHeight && stacked > 0)
                {
                    double fit = BarHeight / stacked;
                    bucket.WarnHeight *= fit;
                    bucket.ErrorHeight *= fit;
                    bucket.FatalHeight *= fit;
                }

                if (bucket.TotalCount > 0 && rateScale > 0)
                    bucket.RateHeight = Math.Max(2, bucket.TotalCount * rateScale);
                if (bucket.RateHeight > BarHeight)
                    bucket.RateHeight = BarHeight;
            }
        }

        private static void FormatToolTips(IList<ErrorTimelineBucket> buckets, string dateFormat)
        {
            var format = string.IsNullOrEmpty(dateFormat) ? "dd/MM/yyyy HH:mm:ss.fff" : dateFormat;
            for (int i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                double seconds = (bucket.To - bucket.From).TotalSeconds;
                // Zero-length slice (every event on the same tick): never divide by zero.
                double rate = seconds > 0 ? bucket.TotalCount / seconds : 0;
                bucket.ToolTip = string.Format(
                    Locals.ErrorTimelineToolTip,
                    bucket.From.ToString(format),
                    bucket.To.ToString(format),
                    bucket.TotalCount,
                    bucket.Warn,
                    bucket.Error,
                    bucket.Fatal,
                    rate);
            }
        }
    }
}
