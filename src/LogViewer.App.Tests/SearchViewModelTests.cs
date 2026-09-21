using System;
using LogViewer.Adapters;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class SearchViewModelTests
    {
        [Test]
        public void FindNext_WrapsAround()
        {
            var env = Create();
            env.State.Logs = new AsyncObservableCollection<LogMessage>
            {
                Msg("aaa"),
                Msg("needle"),
                Msg("ccc")
            };
            env.State.SelectedLog = env.State.Logs[1];
            env.Search.SearchText = "needle";

            env.Search.FindNextCommand.Execute(null);
            Assert.That(env.State.SelectedLog, Is.SameAs(env.State.Logs[1]));
        }

        [Test]
        public void FindPrevious_FromFirstMatch_DoesNotWrap()
        {
            var env = Create();
            env.State.Logs = new AsyncObservableCollection<LogMessage>
            {
                Msg("needle"),
                Msg("aaa")
            };
            env.State.SelectedLog = env.State.Logs[0];
            env.Search.SearchText = "needle";

            env.Search.FindPreviousCommand.Execute(null);
            Assert.That(env.State.SelectedLog, Is.SameAs(env.State.Logs[0]));
        }

        [Test]
        public void SearchInNewWindow_UsesTheVisibleListRow()
        {
            var env = Create();
            var time = new DateTime(2026, 9, 22, 1, 2, 3);
            var other = new LogMessage { Message = "other", Level = eLogLevel.Info, Time = time, Address = "127.0.0.1", Logger = "App", Thread = 1 };
            var visible = new LogMessage { Message = "needle", Level = eLogLevel.Info, Time = time.AddSeconds(1), Address = "127.0.0.1", Logger = "App", Thread = 2 };
            env.State.Logs = new AsyncObservableCollection<LogMessage> { other, visible };
            env.Session.AddEntry(new LogEntry { Message = "other", Level = LogLevel.Info, Time = time, Address = "127.0.0.1", Logger = "App", Thread = 1 });
            env.Session.AddEntry(new LogEntry { Message = "needle", Level = LogLevel.Info, Time = time.AddSeconds(1), Address = "127.0.0.1", Logger = "App", Thread = 2 });
            env.Search.SearchText = "needle";

            env.Search.SearchLogCommand.Execute(true);

            Assert.That(env.Dialogs.LastSearchResults, Has.Count.EqualTo(1));
            Assert.That(env.Dialogs.LastSearchResults[0], Is.SameAs(visible));
            env.Dialogs.LastShowLog(env.Dialogs.LastSearchResults[0]);
            Assert.That(env.State.SelectedLog, Is.SameAs(visible));
        }

        [Test]
        public void ClearSearchResult_ClearsText()
        {
            var env = Create();
            env.Search.SearchText = "x";
            env.Search.ClearSearchResultCommand.Execute(null);
            Assert.That(env.Search.SearchText, Is.Empty);
        }

        private static SearchEnv Create()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var state = new LogViewState();
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
            var settings = new FakeAppSettings();
            var dialogs = new FakeDialogs();
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var search = new SearchViewModel(state, coordinator, session, processing, projector, new LogQueryService(), dialogs, settings, () => eLogLevel.Trace);
            return new SearchEnv { State = state, Search = search, Session = session, Dialogs = dialogs };
        }

        private static LogMessage Msg(string text)
        {
            return new LogMessage
            {
                Message = text,
                Level = eLogLevel.Info,
                Time = DateTime.UtcNow,
                Address = "127.0.0.1",
                Logger = "App"
            };
        }

        private sealed class SearchEnv
        {
            public LogViewState State;
            public SearchViewModel Search;
            public LogSession Session;
            public FakeDialogs Dialogs;
        }
    }
}
