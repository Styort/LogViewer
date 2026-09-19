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
        /// <summary>Stacked bar cap in px; Core buckets have no heights.</summary>
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
            int maxTotal = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                int total = buckets[i].Warn + buckets[i].Error + buckets[i].Fatal;
                if (total > maxTotal)
                    maxTotal = total;
            }

            if (maxTotal <= 0)
                return;

            double scale = 28d / maxTotal;
            for (int i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                // Floor 2px: a single Warn in a busy window would otherwise be a 0-height bar.
                if (bucket.Warn > 0)
                    bucket.WarnHeight = Math.Max(2, bucket.Warn * scale);
                if (bucket.Error > 0)
                    bucket.ErrorHeight = Math.Max(2, bucket.Error * scale);
                if (bucket.Fatal > 0)
                    bucket.FatalHeight = Math.Max(2, bucket.Fatal * scale);

                double stacked = bucket.WarnHeight + bucket.ErrorHeight + bucket.FatalHeight;
                if (stacked > 28 && stacked > 0)
                {
                    double fit = 28 / stacked;
                    bucket.WarnHeight *= fit;
                    bucket.ErrorHeight *= fit;
                    bucket.FatalHeight *= fit;
                }
            }
        }

        private static void FormatToolTips(IList<ErrorTimelineBucket> buckets, string dateFormat)
        {
            var format = string.IsNullOrEmpty(dateFormat) ? "dd/MM/yyyy HH:mm:ss.fff" : dateFormat;
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
