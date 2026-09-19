using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LogEntryBatcherTests
    {
        [Test]
        public void Flush_DeliversEntriesInReceiveOrder()
        {
            var flushed = new List<string>();
            using (var batcher = new LogEntryBatcher((batch, epoch) =>
            {
                foreach (var item in batch)
                    flushed.Add(item.Entry.Message);
            }, flushIntervalMs: 60_000, flushThreshold: 100))
            {
                batcher.Enqueue(Processed("a"));
                batcher.Enqueue(Processed("b"));
                batcher.Enqueue(Processed("c"));
                batcher.Flush();
            }

            Assert.That(flushed, Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void Discard_DropsUnsentQueue_SoClearDoesNotPaintDroppedRows()
        {
            var flushed = new List<string>();
            using (var batcher = new LogEntryBatcher((batch, epoch) =>
            {
                foreach (var item in batch)
                    flushed.Add(item.Entry.Message);
            }, flushIntervalMs: 60_000, flushThreshold: 100))
            {
                batcher.Enqueue(Processed("gone"));
                batcher.Discard();
                batcher.Flush();
                batcher.Enqueue(Processed("kept"));
                batcher.Flush();
            }

            Assert.That(flushed, Is.EqualTo(new[] { "kept" }));
        }

        [Test]
        public void Threshold_FlushesImmediately_WithoutWaitingForTimer()
        {
            var flushed = new List<int>();
            using (var batcher = new LogEntryBatcher((batch, epoch) => flushed.Add(batch.Count),
                flushIntervalMs: 60_000, flushThreshold: 2))
            {
                batcher.Enqueue(Processed("1"));
                batcher.Enqueue(Processed("2"));
            }

            Assert.That(flushed, Is.EqualTo(new[] { 2 }));
        }

        private static LogEntryProcessedEventArgs Processed(string message)
        {
            return new LogEntryProcessedEventArgs
            {
                Entry = new LogEntry { Message = message, Logger = "A", Address = "ip" },
                IncludedInFilter = true
            };
        }
    }
}
