using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LiveAlertEvaluatorTests
    {
        [Test]
        public void EmptyOrNullBatch_IsNotAlertable()
        {
            Assert.That(LiveAlertEvaluator.BatchContainsAlertableEntry(null), Is.False);
            Assert.That(LiveAlertEvaluator.BatchContainsAlertableEntry(new List<LogEntryProcessedEventArgs>()), Is.False);
        }

        [Test]
        public void WarnInfoTraceDebug_DoNotAlert()
        {
            Assert.That(LiveAlertEvaluator.BatchContainsAlertableEntry(new[]
            {
                Processed(LogLevel.Trace),
                Processed(LogLevel.Debug),
                Processed(LogLevel.Info),
                Processed(LogLevel.Warn)
            }), Is.False);
        }

        [Test]
        public void ErrorOrFatal_Alerts_EvenWhenHiddenByDisplayFilter()
        {
            Assert.That(LiveAlertEvaluator.BatchContainsAlertableEntry(new[]
            {
                Processed(LogLevel.Error, includedInFilter: false)
            }), Is.True);
            Assert.That(LiveAlertEvaluator.BatchContainsAlertableEntry(new[]
            {
                Processed(LogLevel.Fatal, includedInFilter: false)
            }), Is.True);
        }

        [Test]
        public void BatchOfManyErrors_IsStillOnePositiveDecision()
        {
            var batch = new List<LogEntryProcessedEventArgs>(50);
            for (int i = 0; i < 50; i++)
                batch.Add(Processed(LogLevel.Error));
            Assert.That(LiveAlertEvaluator.BatchContainsAlertableEntry(batch), Is.True);
        }

        [Test]
        public void MixedWarnAndError_AlertsOnceForTheBatch()
        {
            Assert.That(LiveAlertEvaluator.BatchContainsAlertableEntry(new[]
            {
                Processed(LogLevel.Warn),
                Processed(LogLevel.Error),
                Processed(LogLevel.Warn)
            }), Is.True);
        }

        [Test]
        public void NotifyThrottle_SuppressesBurst_ThenAllowsAfterWindow()
        {
            var evaluator = new LiveAlertEvaluator();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var throttle = TimeSpan.FromSeconds(4);

            Assert.That(evaluator.TryConsumeNotify(t0, throttle), Is.True);
            Assert.That(evaluator.TryConsumeNotify(t0.AddSeconds(1), throttle), Is.False);
            Assert.That(evaluator.TryConsumeNotify(t0.AddSeconds(3), throttle), Is.False);
            Assert.That(evaluator.TryConsumeNotify(t0.AddSeconds(4), throttle), Is.True);
        }

        private static LogEntryProcessedEventArgs Processed(LogLevel level, bool includedInFilter = true)
        {
            return new LogEntryProcessedEventArgs
            {
                Entry = new LogEntry { Level = level, Message = "x", Logger = "A", Address = "ip" },
                IncludedInFilter = includedInFilter
            };
        }
    }
}
