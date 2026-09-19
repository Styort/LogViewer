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
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var search = new SearchViewModel(state, coordinator, session, processing, projector, new LogQueryService(), new FakeDialogs(), settings, () => eLogLevel.Trace);
            return new SearchEnv { State = state, Search = search };
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
        }
    }
}
