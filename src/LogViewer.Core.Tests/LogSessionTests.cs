using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LogSessionTests
    {
        [Test]
        public void AddEntryAndAddEntries_AssignIncreasingSequence_NotResetByClear()
        {
            var session = new LogSession();
            var a = Entry("A");
            var b = Entry("B");
            var c = Entry("C");

            session.AddEntry(a);
            session.AddEntries(new List<LogEntry> { b, c });
            session.Clear();
            var d = Entry("D");
            session.AddEntry(d);

            Assert.That(a.Sequence, Is.EqualTo(1));
            Assert.That(b.Sequence, Is.EqualTo(2));
            Assert.That(c.Sequence, Is.EqualTo(3));
            Assert.That(d.Sequence, Is.EqualTo(4), "a sequence is never reused");
        }

        [Test]
        public void GetSnapshot_ReturnsLastAssignedSequence()
        {
            var session = new LogSession();
            session.AddEntries(new List<LogEntry> { Entry("A"), Entry("B") });

            var snapshot = session.GetSnapshot(out long lastSequence);

            Assert.That(snapshot.Count, Is.EqualTo(2));
            Assert.That(lastSequence, Is.EqualTo(snapshot.Last().Sequence));
        }

        [Test]
        public void Trim_KeepsSequenceOrderOfRemainingEntries()
        {
            var session = new LogSession
            {
                AllowMaxMessageBufferSize = true,
                MaxMessageBufferSize = 3,
                DeletedMessagesCount = 2
            };
            for (int i = 0; i < 5; i++)
                session.AddEntry(Entry("L" + i));

            var sequences = session.GetAllEntries().Select(e => e.Sequence).ToList();

            Assert.That(sequences, Is.Ordered.Ascending);
            Assert.That(sequences.Last(), Is.EqualTo(5));
        }

        [Test]
        public void Filter_CancelledToken_Throws()
        {
            var entries = Enumerable.Range(0, 5000).Select(i => Entry("L" + i)).ToList();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.That(
                    () => LogSession.Filter(entries, new LogFilter(), new FilterCriteria(), cts.Token),
                    Throws.InstanceOf<OperationCanceledException>());
            }
        }

        [Test]
        public void GetFilteredEntries_DoesNotBlockAddEntryOnAnotherThread()
        {
            var session = new LogSession();
            session.AddEntries(Enumerable.Range(0, 1000).Select(i => Entry("L" + i)).ToList());
            var slow = new BlockingFilter();

            var filtering = new Thread(() => session.GetFilteredEntries(slow)) { IsBackground = true };
            filtering.Start();
            try
            {
                Assert.That(slow.Entered.Wait(3000), Is.True);

                // The filter pass is parked inside ShouldInclude; a receive thread must still be able to add.
                var add = new Thread(() => session.AddEntry(Entry("live"))) { IsBackground = true };
                add.Start();
                Assert.That(add.Join(3000), Is.True, "AddEntry waited for the filter pass");
            }
            finally
            {
                slow.Release.Set();
                filtering.Join(3000);
            }
        }

        private static LogEntry Entry(string logger)
        {
            return new LogEntry { Logger = logger, Address = "10.0.0.1", Level = LogLevel.Info, Message = "m", Time = DateTime.Now };
        }

        private sealed class BlockingFilter : Abstractions.ILogFilter
        {
            public readonly ManualResetEventSlim Entered = new ManualResetEventSlim(false);
            public readonly ManualResetEventSlim Release = new ManualResetEventSlim(false);

            public bool ShouldInclude(LogEntry entry, FilterCriteria criteria)
            {
                Entered.Set();
                Release.Wait(5000);
                return true;
            }
        }
    }
}
