using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LogViewer.Adapters;
using LogViewer.Core.Domain;
using LogViewer.Core.State;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class LogSessionPresenterTests
    {
        [Test]
        public void MapFilteredToAllLogs_ReusesRowInstancesAndSkipsEntriesNotInAllLogs()
        {
            var session = new LogSession();
            var entries = Enumerable.Range(0, 6).Select(i => Entry("L" + i)).ToList();
            session.AddEntries(entries);
            var settings = new FakeAppSettings();
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var state = new LogViewState();
            // Row 0 was trimmed, row 5 is still in a pending batch.
            var rows = entries.Skip(1).Take(4).Select(projector.Project).ToList();
            state.AllLogs = new AsyncObservableCollection<LogMessage>(rows);
            var presenter = new LogSessionPresenter(state, projector, null, null, null, null, _ => { });

            var filtered = presenter.MapFilteredToAllLogs(new[] { entries[0], entries[2], entries[4], entries[5] });

            Assert.That(filtered, Has.Count.EqualTo(2));
            Assert.That(filtered[0], Is.SameAs(rows[1]));
            Assert.That(filtered[1], Is.SameAs(rows[3]));
        }

        [Test]
        public void MapFilteredToAllLogs_AllLogsOutOfSequenceOrder_StillMatchesEveryRow()
        {
            // Two receive threads may enqueue in the opposite order from the one they stored entries in.
            var session = new LogSession();
            var entries = Enumerable.Range(0, 4).Select(i => Entry("L" + i)).ToList();
            session.AddEntries(entries);
            var settings = new FakeAppSettings();
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var state = new LogViewState();
            var rows = new List<LogMessage>
            {
                projector.Project(entries[0]),
                projector.Project(entries[2]),
                projector.Project(entries[1]),
                projector.Project(entries[3])
            };
            state.AllLogs = new AsyncObservableCollection<LogMessage>(rows);
            var presenter = new LogSessionPresenter(state, projector, null, null, null, null, _ => { });

            var filtered = presenter.MapFilteredToAllLogs(entries);

            Assert.That(filtered, Has.Count.EqualTo(4));
            Assert.That(filtered[1], Is.SameAs(rows[2]));
            Assert.That(filtered[2], Is.SameAs(rows[1]));
        }

        [Test]
        public void HasSameRows_SameInstancesInOrder_IsTrue_OtherwiseFalse()
        {
            // Start and repeated Apply must not replace Logs: a new collection resets the ListView scroll.
            var a = new LogMessage();
            var b = new LogMessage();
            var current = new AsyncObservableCollection<LogMessage>(new[] { a, b });

            Assert.That(LogSessionPresenter.HasSameRows(current, new List<LogMessage> { a, b }), Is.True);
            Assert.That(LogSessionPresenter.HasSameRows(current, new List<LogMessage> { b, a }), Is.False);
            Assert.That(LogSessionPresenter.HasSameRows(current, new List<LogMessage> { a }), Is.False);
            Assert.That(LogSessionPresenter.HasSameRows(current, new List<LogMessage> { a, new LogMessage() }), Is.False);
        }

        [Test]
        public void Project_RowsOfOneReceiverShareDisplayReceiverAndProperties()
        {
            var settings = new FakeAppSettings();
            settings.Receivers.Add(new Receiver { Port = 7071, Transport = ReceiverTransport.Udp, Name = "Main" });
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var a = Entry("A");
            var b = Entry("B");

            var rowA = projector.Project(a);
            var rowB = projector.Project(b);

            Assert.That(rowA.Receiver, Is.SameAs(rowB.Receiver));
            Assert.That(rowA.Receiver, Is.Not.SameAs(settings.Receivers[0]), "editing settings must not recolor rows before OK");
            Assert.That(rowA.Receiver.Name, Is.EqualTo("Main"));
            Assert.That(rowA.Properties, Is.SameAs(a.Properties));
        }

        private static LogEntry Entry(string logger)
        {
            return new LogEntry
            {
                Logger = logger,
                Address = "10.0.0.1",
                Level = LogLevel.Info,
                Message = "m",
                Time = DateTime.Now,
                ReceiverPort = 7071,
                ReceiverTransport = ReceiverTransport.Udp
            };
        }
    }
}
