using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class HighlightEngineTests
    {
        [Test]
        public void EmptyRules_ReturnNull()
        {
            var engine = new HighlightEngine();
            Assert.That(engine.TryMatch(Entry(LogLevel.Error, "boom")), Is.Null);
        }

        [Test]
        public void LevelError_MatchesError_NotInfo()
        {
            var engine = Engine(Rule(level: "Error", color: "#4DFF0000"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Error, "x")), Is.EqualTo("#4DFF0000"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Fatal, "x")), Is.Null);
            Assert.That(engine.TryMatch(Entry(LogLevel.Info, "x")), Is.Null);
        }

        [Test]
        public void LoggerSubstring_IsCaseInsensitive()
        {
            var engine = Engine(Rule(logger: "Payment", color: "#3300FF00"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Info, "ok", logger: "App.payment.Api")), Is.EqualTo("#3300FF00"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Info, "ok", logger: "Other")), Is.Null);
        }

        [Test]
        public void LoggerRegex_Matches()
        {
            var engine = Engine(new HighlightRule
            {
                Enabled = true,
                LoggerPattern = @"Pay(ment)?\.Api",
                LoggerIsRegex = true,
                ColorArgb = "#330000FF"
            });
            Assert.That(engine.TryMatch(Entry(LogLevel.Info, "ok", logger: "Payment.Api")), Is.EqualTo("#330000FF"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Info, "ok", logger: "Other")), Is.Null);
        }

        [Test]
        public void MessageRegex_Matches()
        {
            var engine = Engine(Rule(message: "time.?out", messageRegex: true, color: "#33FF00FF"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Warn, "timeout")), Is.EqualTo("#33FF00FF"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Warn, "time-out")), Is.EqualTo("#33FF00FF"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Warn, "ok")), Is.Null);
        }

        [Test]
        public void DisabledRule_IsIgnored()
        {
            var engine = Engine(new HighlightRule
            {
                Enabled = false,
                Level = "Error",
                ColorArgb = "#4DFF0000"
            });
            Assert.That(engine.TryMatch(Entry(LogLevel.Error, "x")), Is.Null);
        }

        [Test]
        public void Order_FirstMatchWins()
        {
            var engine = Engine(
                Rule(level: "Error", color: "#4DFF0000"),
                Rule(level: "Error", color: "#4D0000FF"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Error, "x")), Is.EqualTo("#4DFF0000"));
        }

        [Test]
        public void DisabledFirst_FallsThroughToNext()
        {
            var first = Rule(level: "Error", color: "#4DFF0000");
            first.Enabled = false;
            var engine = Engine(first, Rule(level: "Error", color: "#4D0000FF"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Error, "x")), Is.EqualTo("#4D0000FF"));
        }

        [Test]
        public void InvalidRegex_DoesNotThrow_AndDoesNotMatch()
        {
            var engine = new HighlightEngine();
            Assert.DoesNotThrow(() => engine.ReplaceRules(new[]
            {
                Rule(message: "(", messageRegex: true, color: "#4DFF0000")
            }));
            Assert.That(engine.TryMatch(Entry(LogLevel.Info, "(")), Is.Null);
        }

        [Test]
        public void LevelAndLogger_AreAnd()
        {
            var engine = Engine(Rule(level: "Error", logger: "Pay", color: "#4DFF0000"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Error, "x", logger: "Pay.Api")), Is.EqualTo("#4DFF0000"));
            Assert.That(engine.TryMatch(Entry(LogLevel.Error, "x", logger: "Other")), Is.Null);
            Assert.That(engine.TryMatch(Entry(LogLevel.Warn, "x", logger: "Pay.Api")), Is.Null);
        }

        private static HighlightEngine Engine(params HighlightRule[] rules)
        {
            var engine = new HighlightEngine();
            engine.ReplaceRules(rules);
            return engine;
        }

        private static HighlightRule Rule(
            string level = null,
            string logger = null,
            string message = null,
            bool messageRegex = false,
            string color = "#4DFF0000")
        {
            return new HighlightRule
            {
                Enabled = true,
                Level = level,
                LoggerPattern = logger,
                MessagePattern = message,
                MessageIsRegex = messageRegex,
                ColorArgb = color
            };
        }

        private static LogEntry Entry(LogLevel level, string message, string logger = "App")
        {
            return new LogEntry { Level = level, Message = message, Logger = logger, Address = "ip" };
        }
    }
}
