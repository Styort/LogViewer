using System;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LogFilterTests
    {
        [Test]
        public void ShouldInclude_RequiresTimeAndLevelAndSearch()
        {
            var filter = new LogFilter();
            var t = new DateTime(2020, 1, 2, 12, 0, 0);
            var criteria = new FilterCriteria
            {
                MinLevel = LogLevel.Warn,
                IsTimeIntervalActive = true,
                TimeRangeFrom = t.AddMinutes(-1),
                TimeRangeTo = t.AddMinutes(1),
                IsSearchActive = true,
                SearchText = "timeout"
            };

            var hit = Entry(LogLevel.Warn, "timeout", t, logger: "App");
            var wrongLevel = Entry(LogLevel.Info, "timeout", t, logger: "App");
            var wrongTime = Entry(LogLevel.Warn, "timeout", t.AddHours(2), logger: "App");
            var wrongSearch = Entry(LogLevel.Warn, "ok", t, logger: "App");

            Assert.That(filter.ShouldInclude(hit, criteria), Is.True);
            Assert.That(filter.ShouldInclude(wrongLevel, criteria), Is.False);
            Assert.That(filter.ShouldInclude(wrongTime, criteria), Is.False);
            Assert.That(filter.ShouldInclude(wrongSearch, criteria), Is.False);
        }

        [Test]
        public void Search_MatchesLoggerAddressAndThread()
        {
            var matcher = SearchMatcher.Create("needle", false, false, false);
            Assert.That(matcher.Matches(Entry(LogLevel.Info, "other", logger: "needle")), Is.True);
            Assert.That(matcher.Matches(new LogEntry { Address = "needle", Logger = "A", Message = "x" }), Is.True);
            Assert.That(matcher.Matches(new LogEntry { Thread = 42, Logger = "A", Message = "x", Address = "ip" }), Is.False);

            var threadHit = SearchMatcher.Create("42", false, false, false);
            Assert.That(threadHit.Matches(new LogEntry { Thread = 42, Logger = "A", Message = "x", Address = "ip" }), Is.True);
        }

        [Test]
        public void WholeWord_DoesNotMatchSubstring()
        {
            var matcher = SearchMatcher.Create("err", false, false, true);
            Assert.That(matcher.Matches(Entry(LogLevel.Info, "error")), Is.False);
            Assert.That(matcher.Matches(Entry(LogLevel.Info, "an err here")), Is.True);
        }

        [Test]
        public void InvalidRegex_IsDetectable_AndDoesNotEmptyTheList()
        {
            var matcher = SearchMatcher.Create("(", false, true, false);
            Assert.That(matcher.IsPatternInvalid, Is.True);

            var filter = new LogFilter();
            var criteria = new FilterCriteria
            {
                IsSearchActive = true,
                UseRegex = true,
                SearchText = "(",
                IsSearchPatternInvalid = true
            };
            Assert.That(filter.ShouldInclude(Entry(LogLevel.Info, "anything"), criteria), Is.True);
        }

        [Test]
        public void ExcludedLogger_IsHiddenFromDisplay()
        {
            var filter = new LogFilter();
            var entry = Entry(LogLevel.Info, "hi", logger: "My.App");
            var criteria = new FilterCriteria();
            criteria.ExcludedLoggerFullPaths.Add(entry.FullPath);

            Assert.That(filter.ShouldInclude(entry, criteria), Is.False);
        }

        [Test]
        public void IncludeOnly_HidesLoggersOutsideSelectedRoots()
        {
            var filter = new LogFilter();
            const string file = @"C:\Users\styor\Downloads\2026-09-18.txt";
            var criteria = new FilterCriteria();
            criteria.IncludedLoggerFullPaths.Add("SecurityLog");
            criteria.IncludedLoggerFullPaths.Add("Terminal");

            Assert.That(filter.ShouldInclude(new LogEntry { Address = file, Logger = "SecurityLog", Message = "a", Level = LogLevel.Info }, criteria), Is.True);
            Assert.That(filter.ShouldInclude(new LogEntry { Address = file, Logger = "SecurityLog.SecurityLogService", Message = "a", Level = LogLevel.Info }, criteria), Is.True);
            Assert.That(filter.ShouldInclude(new LogEntry { Address = file, Logger = "Terminal", Message = "a", Level = LogLevel.Info }, criteria), Is.True);
            Assert.That(filter.ShouldInclude(new LogEntry { Address = file, Logger = "App", Message = "a", Level = LogLevel.Info }, criteria), Is.False);
        }

        private static LogEntry Entry(LogLevel level, string message, DateTime? time = null, string logger = "App")
        {
            return new LogEntry
            {
                Address = "127.0.0.1",
                Logger = logger,
                Message = message,
                Level = level,
                Time = time ?? new DateTime(2020, 1, 2, 12, 0, 0)
            };
        }
    }
}
