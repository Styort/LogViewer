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

        private static List<TimelineEvent> Events(DateTime time, LogLevel level)
        {
            return new List<TimelineEvent>
            {
                new TimelineEvent { Time = time, Level = level, Index = 0 }
            };
        }
    }
}
