using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LogFilterPropertySearchTests
    {
        [Test]
        public void Search_FindsPropertyValue_WhenMessageDoesNotContainIt()
        {
            var entry = new LogEntry
            {
                Message = "unrelated",
                Logger = "App",
                Address = "127.0.0.1",
                Properties = { { "user", "alice" } }
            };

            var matcher = SearchMatcher.Create("alice", matchCase: false, useRegex: false, matchWholeWord: false);
            Assert.That(matcher.Matches(entry), Is.True);
        }

        [Test]
        public void Search_FindsThrowableText()
        {
            var entry = new LogEntry
            {
                Message = "failed",
                Logger = "App",
                Throwable = "System.Exception: boom"
            };

            var matcher = SearchMatcher.Create("boom", matchCase: false, useRegex: false, matchWholeWord: false);
            Assert.That(matcher.Matches(entry), Is.True);
        }

        [Test]
        public void Filter_IncludesEntryWhenPropertyMatches()
        {
            var filter = new LogFilter();
            var criteria = new FilterCriteria
            {
                IsSearchActive = true,
                SearchText = "alice",
                MatchLogLevel = false
            };
            var entry = new LogEntry
            {
                Level = LogLevel.Info,
                Message = "other",
                Logger = "App",
                Properties = new Dictionary<string, string> { { "user", "alice" } }
            };

            Assert.That(filter.ShouldInclude(entry, criteria), Is.True);
        }
    }
}
