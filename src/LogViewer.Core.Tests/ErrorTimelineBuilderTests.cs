using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class ErrorTimelineBuilderTests
    {
        [Test]
        public void EmptyInput_ReturnsEmpty()
        {
            Assert.That(ErrorTimelineBuilder.Build(null, 10), Is.Empty);
            Assert.That(ErrorTimelineBuilder.Build(new List<TimelineEvent>(), 10), Is.Empty);
            Assert.That(ErrorTimelineBuilder.Build(Events(DateTime.UtcNow, LogLevel.Error), 0), Is.Empty);
        }

        [Test]
        public void SingleEvent_OneBucketCoveringThatInstant()
        {
            var t = new DateTime(2020, 1, 1, 0, 0, 0);
            var buckets = ErrorTimelineBuilder.Build(Events(t, LogLevel.Error), 120);

            Assert.That(buckets.Count, Is.EqualTo(1));
            Assert.That(buckets[0].From, Is.EqualTo(t));
            Assert.That(buckets[0].To, Is.EqualTo(t));
            Assert.That(buckets[0].Error, Is.EqualTo(1));
            Assert.That(buckets[0].FirstHitIndex, Is.EqualTo(0));
        }

        [Test]
        public void MinAndMaxLandInFirstAndLastBuckets()
        {
            var min = new DateTime(2020, 1, 1, 0, 0, 0);
            var max = new DateTime(2020, 1, 1, 1, 0, 0);
            var events = new List<TimelineEvent>
            {
                new TimelineEvent { Time = min, Level = LogLevel.Warn, Index = 0 },
                new TimelineEvent { Time = max, Level = LogLevel.Fatal, Index = 1 }
            };

            var buckets = ErrorTimelineBuilder.Build(events, 10);

            Assert.That(buckets.Count, Is.EqualTo(10));
            Assert.That(buckets[0].From, Is.EqualTo(min));
            Assert.That(buckets[0].Warn, Is.EqualTo(1));
            Assert.That(buckets[0].FirstHitIndex, Is.EqualTo(0));
            Assert.That(buckets[9].To, Is.EqualTo(max));
            Assert.That(buckets[9].Fatal, Is.EqualTo(1));
            Assert.That(buckets[9].FirstHitIndex, Is.EqualTo(1));
        }

        [Test]
        public void BucketCountChange_ChangesBucketCount()
        {
            var min = new DateTime(2020, 1, 1, 0, 0, 0);
            var max = new DateTime(2020, 1, 1, 1, 0, 0);
            var events = new List<TimelineEvent>
            {
                new TimelineEvent { Time = min, Level = LogLevel.Error, Index = 0 },
                new TimelineEvent { Time = max, Level = LogLevel.Error, Index = 1 }
            };

            Assert.That(ErrorTimelineBuilder.Build(events, 40).Count, Is.EqualTo(40));
            Assert.That(ErrorTimelineBuilder.Build(events, 200).Count, Is.EqualTo(200));
        }

        [Test]
        public void InfoEvents_CountTowardTotal_NotErrors()
        {
            var min = new DateTime(2020, 1, 1, 0, 0, 0);
            var max = new DateTime(2020, 1, 1, 0, 0, 10);
            var events = new List<TimelineEvent>
            {
                new TimelineEvent { Time = min, Level = LogLevel.Info, Index = 0 },
                new TimelineEvent { Time = min.AddSeconds(1), Level = LogLevel.Debug, Index = 1 },
                new TimelineEvent { Time = max, Level = LogLevel.Info, Index = 2 }
            };

            var buckets = ErrorTimelineBuilder.Build(events, 10);

            Assert.That(buckets.Count, Is.EqualTo(10));
            int total = 0;
            int errors = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                total += buckets[i].TotalCount;
                errors += buckets[i].Warn + buckets[i].Error + buckets[i].Fatal;
            }

            Assert.That(total, Is.EqualTo(3));
            Assert.That(errors, Is.EqualTo(0));
            Assert.That(buckets[0].From, Is.EqualTo(min));
            Assert.That(buckets[9].To, Is.EqualTo(max));
            Assert.That(buckets[0].FirstHitIndex, Is.EqualTo(0));
        }

        [Test]
        public void WarnPlus_IsIncludedInTotalCount()
        {
            var t = new DateTime(2020, 1, 1, 0, 0, 0);
            var buckets = ErrorTimelineBuilder.Build(
                new List<TimelineEvent>
                {
                    new TimelineEvent { Time = t, Level = LogLevel.Info, Index = 0 },
                    new TimelineEvent { Time = t, Level = LogLevel.Error, Index = 1 }
                },
                8);

            Assert.That(buckets.Count, Is.EqualTo(1));
            Assert.That(buckets[0].TotalCount, Is.EqualTo(2));
            Assert.That(buckets[0].Error, Is.EqualTo(1));
            Assert.That(buckets[0].FirstHitIndex, Is.EqualTo(0));
        }

        [Test]
        public void MessagesPerSecond_UsesBucketDuration()
        {
            var min = new DateTime(2020, 1, 1, 0, 0, 0);
            var max = new DateTime(2020, 1, 1, 0, 0, 10);
            var events = new List<TimelineEvent>
            {
                new TimelineEvent { Time = min, Level = LogLevel.Info, Index = 0 },
                new TimelineEvent { Time = min, Level = LogLevel.Info, Index = 1 },
                new TimelineEvent { Time = max, Level = LogLevel.Info, Index = 2 }
            };

            var buckets = ErrorTimelineBuilder.Build(events, 10);
            var first = buckets[0];
            double seconds = (first.To - first.From).TotalSeconds;

            Assert.That(seconds, Is.GreaterThan(0));
            Assert.That(first.TotalCount, Is.EqualTo(2));
            Assert.That(first.MessagesPerSecond, Is.EqualTo(first.TotalCount / seconds).Within(0.0001));
        }

        [Test]
        public void MessagesPerSecond_ZeroDuration_DoesNotDivideByZero()
        {
            var t = new DateTime(2020, 1, 1, 0, 0, 0);
            var buckets = ErrorTimelineBuilder.Build(Events(t, LogLevel.Info), 50);

            Assert.That(buckets.Count, Is.EqualTo(1));
            Assert.That(buckets[0].From, Is.EqualTo(buckets[0].To));
            Assert.That(buckets[0].MessagesPerSecond, Is.EqualTo(0));
            Assert.That(buckets[0].TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void ListOrder_DoesNotChangeTimeAxis()
        {
            var min = new DateTime(2020, 1, 1, 0, 0, 0);
            var max = new DateTime(2020, 1, 1, 1, 0, 0);
            var reversed = new List<TimelineEvent>
            {
                new TimelineEvent { Time = max, Level = LogLevel.Fatal, Index = 0 },
                new TimelineEvent { Time = min, Level = LogLevel.Warn, Index = 1 }
            };

            var buckets = ErrorTimelineBuilder.Build(reversed, 10);

            Assert.That(buckets[0].From, Is.EqualTo(min));
            Assert.That(buckets[0].Warn, Is.EqualTo(1));
            Assert.That(buckets[9].To, Is.EqualTo(max));
            Assert.That(buckets[9].Fatal, Is.EqualTo(1));
        }

        private static List<TimelineEvent> Events(DateTime time, LogLevel level)
        {
            return new List<TimelineEvent>
            {
                new TimelineEvent { Time = time, Level = level, Index = 0 }
            };
        }
    }
}
