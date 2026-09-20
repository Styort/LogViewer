using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class MessageGroupAggregatorTests
    {
        [Test]
        public void EmptyOrNull_ReturnsEmpty()
        {
            Assert.That(MessageGroupAggregator.Build(null), Is.Empty);
            Assert.That(MessageGroupAggregator.Build(new List<LogEntry>()), Is.Empty);
        }

        [Test]
        public void SameErrorWithDifferentGuids_OneGroup()
        {
            var entries = new List<LogEntry>();
            var start = new DateTime(2024, 1, 1, 0, 0, 0);
            for (int i = 0; i < 847; i++)
            {
                entries.Add(new LogEntry
                {
                    Time = start.AddMilliseconds(i),
                    Level = LogLevel.Error,
                    Logger = "Payments",
                    Message = "Timeout " + Guid.NewGuid()
                });
            }

            var groups = MessageGroupAggregator.Build(entries);
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].Count, Is.EqualTo(847));
            Assert.That(groups[0].FirstIndex, Is.EqualTo(0));
            Assert.That(groups[0].LastIndex, Is.EqualTo(846));
            Assert.That(groups[0].FirstTime, Is.EqualTo(start));
            Assert.That(groups[0].LastTime, Is.EqualTo(start.AddMilliseconds(846)));
        }

        [Test]
        public void DifferentLoggers_TwoGroups()
        {
            var t = DateTime.UtcNow;
            var entries = new List<LogEntry>
            {
                new LogEntry { Time = t, Level = LogLevel.Error, Logger = "A", Message = "same" },
                new LogEntry { Time = t.AddSeconds(1), Level = LogLevel.Error, Logger = "B", Message = "same" }
            };

            var groups = MessageGroupAggregator.Build(entries);
            Assert.That(groups.Count, Is.EqualTo(2));
        }

        [Test]
        public void SortedByCountDescending()
        {
            var t = DateTime.UtcNow;
            var entries = new List<LogEntry>
            {
                new LogEntry { Time = t, Level = LogLevel.Info, Logger = "A", Message = "once" },
                new LogEntry { Time = t, Level = LogLevel.Info, Logger = "B", Message = "twice" },
                new LogEntry { Time = t, Level = LogLevel.Info, Logger = "B", Message = "twice" }
            };

            var groups = MessageGroupAggregator.Build(entries);
            Assert.That(groups[0].Count, Is.EqualTo(2));
            Assert.That(groups[0].Logger, Is.EqualTo("B"));
            Assert.That(groups[1].Count, Is.EqualTo(1));
        }
    }
}
