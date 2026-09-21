using System;
using System.IO;
using System.Threading;
using LogViewer.Adapters;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.Factories;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class SessionViewModelTests
    {
        [Test]
        public void Open_InvalidFile_DoesNotClearBuffer()
        {
            var env = Create();
            env.Session.AddEntry(new LogEntry
            {
                Time = DateTime.UtcNow,
                Level = LogLevel.Info,
                Logger = "App",
                Address = "127.0.0.1",
                Message = "keep-me"
            });
            int before = env.Session.EntryCount;

            var path = Path.GetTempFileName();
            File.WriteAllText(path, "<not-a-session>");
            env.Files.OpenFilePath = path;

            env.ViewModel.OpenCommand.Execute(null);

            Assert.That(env.Cleaned, Is.False);
            Assert.That(env.Session.EntryCount, Is.EqualTo(before));
        }

        [Test]
        public void Open_ReplacesBufferAfterConfirm()
        {
            var env = Create();
            env.Session.AddEntry(new LogEntry
            {
                Time = DateTime.UtcNow,
                Level = LogLevel.Info,
                Logger = "Old",
                Address = "127.0.0.1",
                Message = "old"
            });

            var document = new SavedSessionDocument
            {
                Version = "1",
                Entries =
                {
                    SavedSessionMapper.ToDto(new LogEntry
                    {
                        Time = new DateTime(2026, 1, 2, 3, 4, 5),
                        Level = LogLevel.Error,
                        Logger = "New",
                        Address = "10.0.0.1",
                        Message = "loaded",
                        Throwable = "ex",
                        Properties = { { "k", "v" } }
                    })
                },
                DontReceiveLoggerFullPaths = { "10.0.0.1.Noise" }
            };
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".lvs");
            SavedSessionXmlStore.SaveFile(path, document);
            env.Files.OpenFilePath = path;
            env.Dialogs.ConfirmResult = true;

            env.ViewModel.OpenCommand.Execute(null);

            Assert.That(env.Cleaned, Is.True);
            Assert.That(env.Session.EntryCount, Is.EqualTo(1));
            Assert.That(env.Session.GetAllEntries()[0].Message, Is.EqualTo("loaded"));
            Assert.That(env.Session.GetAllEntries()[0].Throwable, Is.EqualTo("ex"));
            Assert.That(env.Session.GetAllEntries()[0].Properties["k"], Is.EqualTo("v"));
            Assert.That(env.Coordinator.Loggers.ExcludedWithBufferPaths, Does.Contain("10.0.0.1.Noise"));
        }

        private static SessionEnv Create()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var state = new LogViewState();
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
            var settings = new FakeAppSettings();
            var dialogs = new FakeDialogs();
            var files = new FakeFiles();
            var adapter = new CoreToUiAdapter(new SynchronizationContext(), processing);
            var receivers = new ReceiversViewModel(processing, adapter, coordinator, new LogSourceFactory(settings), dialogs, settings.Receivers);
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var search = new SearchViewModel(state, coordinator, session, processing, projector, new LogQueryService(), dialogs, settings, () => eLogLevel.Trace);
            var bookmarks = new BookmarksViewModel(state, dialogs);
            var watch = new LogFileWatchService(processing);
            var import = new ImportViewModel(state, session, processing, new LogImportService(session), adapter, watch, dialogs, files, receivers, m => "");
            var tree = new LoggerTreeViewModel(state, coordinator, session, processing, watch, new LoggerTreeBuilder(), new LoggerTreeMarker(state, settings), settings, receivers, import, () => { });
            bool cleaned = false;
            var vm = new SessionViewModel(
                session,
                state,
                coordinator,
                search,
                bookmarks,
                tree,
                dialogs,
                files,
                receivers,
                () =>
                {
                    cleaned = true;
                    session.Clear();
                },
                _ => { });
            return new SessionEnv
            {
                Session = session,
                Coordinator = coordinator,
                Files = files,
                Dialogs = dialogs,
                ViewModel = vm,
                GetCleaned = () => cleaned
            };
        }

        private sealed class SessionEnv
        {
            public LogSession Session;
            public FilterCoordinator Coordinator;
            public FakeFiles Files;
            public FakeDialogs Dialogs;
            public SessionViewModel ViewModel;
            public Func<bool> GetCleaned;
            public bool Cleaned => GetCleaned();
        }
    }
}
