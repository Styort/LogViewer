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
    public class FilterPresetsViewModelTests
    {
        [Test]
        public void ApplyThenClear_TogglesHasActivePresetAndRestoresLevel()
        {
            var env = Create();
            env.ViewModel.Items.Add(new FilterPreset
            {
                Name = "Errors",
                MinLevel = "Error",
                SearchText = "timeout",
                IsSearchActive = true
            });
            env.ViewModel.Selected = env.ViewModel.Items[0];

            env.ViewModel.ApplySelected();

            Assert.That(env.ViewModel.HasActivePreset, Is.True);
            Assert.That(env.ViewModel.ActivePresetName, Is.EqualTo("Errors"));
            Assert.That(env.LastMinLevel, Is.EqualTo(eLogLevel.Error));
            Assert.That(env.Coordinator.CurrentMinLevel, Is.EqualTo(eLogLevel.Error));

            env.ViewModel.ClearActive();

            Assert.That(env.ViewModel.HasActivePreset, Is.False);
            Assert.That(env.Coordinator.CurrentMinLevel, Is.EqualTo(eLogLevel.Trace));
            Assert.That(env.LastMinLevel, Is.EqualTo(eLogLevel.Trace));
        }

        private static PresetEnv Create()
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var state = new LogViewState();
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
            var settings = new FakeAppSettings();
            var dialogs = new FakeDialogs();
            var files = new FakeFiles();
            var sync = new SynchronizationContext();
            var adapter = new CoreToUiAdapter(sync, processing);
            var receivers = new ReceiversViewModel(processing, adapter, coordinator, new LogSourceFactory(settings), dialogs, settings.Receivers);
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var search = new SearchViewModel(state, coordinator, session, processing, projector, new LogQueryService(), dialogs, settings, () => eLogLevel.Trace);
            var watch = new LogFileWatchService(processing);
            var import = new ImportViewModel(state, session, processing, new LogImportService(session), adapter, watch, dialogs, files, receivers, m => "");
            var tree = new LoggerTreeViewModel(state, coordinator, session, processing, watch, new LoggerTreeBuilder(), new LoggerTreeMarker(state, settings), settings, receivers, import, () => { });
            eLogLevel lastLevel = eLogLevel.Trace;
            string path = Path.Combine(Path.GetTempPath(), "logviewer-preset-tests-" + Guid.NewGuid().ToString("N") + ".xml");
            var vm = new FilterPresetsViewModel(coordinator, dialogs, search, tree, level => lastLevel = level, path);
            return new PresetEnv
            {
                Coordinator = coordinator,
                ViewModel = vm,
                GetLastMinLevel = () => lastLevel
            };
        }

        private sealed class PresetEnv
        {
            public FilterCoordinator Coordinator;
            public FilterPresetsViewModel ViewModel;
            public Func<eLogLevel> GetLastMinLevel;
            public eLogLevel LastMinLevel => GetLastMinLevel();
        }
    }
}
