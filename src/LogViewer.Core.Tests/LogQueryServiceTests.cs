using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LogQueryServiceTests
    {
        private readonly LogQueryService _query = new LogQueryService();

        [Test]
        public void FindNext_WrapsFromEndToStart()
        {
            var view = Entries("aaa", "bbb", "needle", "ccc");
            var matcher = SearchMatcher.Create("needle", false, false, false);

            int index = _query.FindNext(view, 2, matcher, LogLevel.Trace);

            Assert.That(index, Is.EqualTo(2));
        }

        [Test]
        public void FindNext_FromNothingSelected_FindsFirst()
        {
            var view = Entries("aaa", "needle", "ccc");
            var matcher = SearchMatcher.Create("needle", false, false, false);

            Assert.That(_query.FindNext(view, -1, matcher, LogLevel.Trace), Is.EqualTo(1));
        }

        [Test]
        public void FindNext_EmptyResult_ReturnsMinusOne()
        {
            var view = Entries("aaa", "bbb");
            var matcher = SearchMatcher.Create("zzz", false, false, false);
            Assert.That(_query.FindNext(view, 0, matcher, LogLevel.Trace), Is.EqualTo(-1));
        }

        [Test]
        public void FindNext_RespectsMinLevel()
        {
            var view = new List<LogEntry>
            {
                Entry("needle", LogLevel.Debug),
                Entry("needle", LogLevel.Error)
            };
            var matcher = SearchMatcher.Create("needle", false, false, false);
            Assert.That(_query.FindNext(view, -1, matcher, LogLevel.Error), Is.EqualTo(1));
        }

        [Test]
        public void FindPrevious_DoesNotWrap()
        {
            var view = Entries("needle", "aaa", "needle");
            var matcher = SearchMatcher.Create("needle", false, false, false);
            Assert.That(_query.FindPrevious(view, 0, matcher, LogLevel.Trace), Is.EqualTo(-1));
            Assert.That(_query.FindPrevious(view, 2, matcher, LogLevel.Trace), Is.EqualTo(0));
        }

        [Test]
        public void FindNextByLevel_ExactMatchOnly()
        {
            var view = new List<LogEntry>
            {
                Entry("a", LogLevel.Info),
                Entry("b", LogLevel.Warn),
                Entry("c", LogLevel.Error)
            };
            Assert.That(_query.FindNextByLevel(view, -1, LogLevel.Error), Is.EqualTo(2));
            Assert.That(_query.FindNextByLevel(view, 1, LogLevel.Warn), Is.EqualTo(-1));
        }

        [Test]
        public void FindByTimestamp_TruncatesMinuteSecondMillisecond()
        {
            var t = new DateTime(2020, 1, 2, 12, 34, 56, 789);
            var view = new List<LogEntry>
            {
                Entry("a", LogLevel.Info, t),
                Entry("b", LogLevel.Info, t.AddMinutes(1))
            };

            Assert.That(_query.FindByTimestamp(view, new DateTime(2020, 1, 2, 12, 34, 0), TimeSpan.FromMinutes(1)), Is.EqualTo(0));
            Assert.That(_query.FindByTimestamp(view, new DateTime(2020, 1, 2, 12, 34, 56), TimeSpan.FromSeconds(1)), Is.EqualTo(0));
            Assert.That(_query.FindByTimestamp(view, t, TimeSpan.FromMilliseconds(1)), Is.EqualTo(0));
            Assert.That(_query.FindByTimestamp(view, t.AddMinutes(1), TimeSpan.FromMinutes(1)), Is.EqualTo(1));
        }

        private static List<LogEntry> Entries(params string[] messages)
        {
            var list = new List<LogEntry>(messages.Length);
            foreach (var m in messages)
                list.Add(Entry(m, LogLevel.Info));
            return list;
        }

        private static LogEntry Entry(string message, LogLevel level, DateTime? time = null)
        {
            return new LogEntry
            {
                Address = "127.0.0.1",
                Logger = "App",
                Message = message,
                Level = level,
                Time = time ?? DateTime.UtcNow
            };
        }
    }
}
