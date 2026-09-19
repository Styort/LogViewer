using System;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    /// <summary>
    /// Don't Receive is buffer exclusion, not display filtering. WPF tree checkboxes are not covered here.
    /// </summary>
    [TestFixture]
    public class DontReceiveTests
    {
        [Test]
        public void AddEntry_IsSkipped_WhenFullPathIsExcludedFromBuffer()
        {
            var session = new LogSession();
            session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer.Add("127.0.0.1.My.App");

            session.AddEntry(Entry("My.App", "keep-me-out"));

            Assert.That(session.EntryCount, Is.EqualTo(0));
            Assert.That(session.GetAllEntries(), Is.Empty);
        }

        [Test]
        public void ProcessingService_DoesNotRaiseEntryProcessed_WhenExcludedFromBuffer()
        {
            var session = new LogSession();
            session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer.Add("127.0.0.1.My.App");
            var service = new LogProcessingService(session, new LogFilter());
            var source = new TestLogSource();
            int processed = 0;
            service.EntryProcessed += (s, e) => processed++;
            service.AddSource(source);

            source.Raise(Entry("My.App", "dropped"));
            source.Raise(Entry("Other", "kept"));

            Assert.That(processed, Is.EqualTo(1));
            Assert.That(session.EntryCount, Is.EqualTo(1));
            Assert.That(session.GetAllEntries()[0].Logger, Is.EqualTo("Other"));
        }

        [Test]
        public void ShouldInclude_DoesNotUseBufferExclusion()
        {
            var filter = new LogFilter();
            var criteria = new FilterCriteria();
            criteria.ExcludedLoggerFullPathsWithBuffer.Add("127.0.0.1.My.App");
            var entry = Entry("My.App", "still displayable if already stored");

            Assert.That(filter.ShouldInclude(entry, criteria), Is.True);
            Assert.That(criteria.ShouldStoreInBuffer(entry), Is.False);
        }

        [Test]
        public void RemoveEntriesExcludedFromBuffer_DropsStoredMessages()
        {
            var session = new LogSession();
            session.AddEntry(Entry("My.App", "a"));
            session.AddEntry(Entry("Other", "b"));
            session.FilterCriteria.ExcludedLoggerFullPathsWithBuffer.Add("127.0.0.1.My.App");
            session.RemoveEntriesExcludedFromBuffer();

            Assert.That(session.GetAllEntries().Count, Is.EqualTo(1));
            Assert.That(session.GetAllEntries()[0].Logger, Is.EqualTo("Other"));
        }

        private static LogEntry Entry(string logger, string message)
        {
            return new LogEntry
            {
                Address = "127.0.0.1",
                Logger = logger,
                Message = message,
                Level = LogLevel.Info,
                Time = DateTime.UtcNow
            };
        }

        private sealed class TestLogSource : ILogSource
        {
            public event EventHandler<LogEntryReceivedEventArgs> LogReceived;

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public void Raise(LogEntry entry)
            {
                LogReceived?.Invoke(this, new LogEntryReceivedEventArgs { Entry = entry });
            }
        }
    }
}
