using System;
using System.Threading;
using LogViewer.Adapters;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.MVVM.ViewModels;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;
using LogViewer.Services.Wpf;

namespace LogViewer.Factories
{
    /// <summary>
    /// Explicit host VM bag. No DI container: tests build the same graph and swap WPF wrappers for fakes.
    /// </summary>
    public sealed class LogViewModelDependencies
    {
        /// <summary>Settings and receiver list — one object per process, like Settings.Instance.</summary>
        public IAppSettings Settings { get; set; }
        /// <summary>MessageBox and modal windows; tests swap in no-ops.</summary>
        public IDialogService Dialogs { get; set; }
        public IFileDialogService Files { get; set; }
        public IClipboardService Clipboard { get; set; }
        /// <summary>Error-timeline timer; tests may leave it unstarted.</summary>
        public IUiTimer TimelineTimer { get; set; }
        /// <summary>Canonical entry buffer. One per window.</summary>
        public LogSession Session { get; set; }
        public LogProcessingService Processing { get; set; }
        public CoreToUiAdapter Adapter { get; set; }
        public ILogImportService ImportService { get; set; }
        public UdpSourceFactory UdpFactory { get; set; }
        public LogQueryService Query { get; set; }
        public LogViewState ViewState { get; set; }
        public FilterCoordinator Coordinator { get; set; }
        public LogEntryProjector Projector { get; set; }
        public LogFileWatchService FileWatch { get; set; }
        public LoggerTreeBuilder TreeBuilder { get; set; }
        public LoggerTreeMarker TreeMarker { get; set; }
    }

    /// <summary>
    /// Main-window composition root. Call only on the UI thread:
    /// the adapter needs <c>SynchronizationContext.Current</c> to Post into the ListView.
    /// </summary>
    public static class LogViewModelFactory
    {
        /// <summary>Production host for MainWindow. Requires the UI SynchronizationContext.</summary>
        public static LogViewModel Create()
        {
            return new LogViewModel(CreateDependencies());
        }

        /// <summary>
        /// Wires Core + WPF implementations. Tests call this and then replace Dialogs/Settings/Clipboard.
        /// </summary>
        public static LogViewModelDependencies CreateDependencies()
        {
            var settings = new WpfAppSettings();
            var dialogs = new WpfDialogService();
            var files = new WpfFileDialogService();
            var clipboard = new WpfClipboardService();
            var timer = new DispatcherUiTimer();

            var session = new LogSession();
            session.AllowMaxMessageBufferSize = settings.IsEnabledMaxMessageBufferSize;
            session.MaxMessageBufferSize = settings.MaxMessageBufferSize;
            session.DeletedMessagesCount = settings.DeletedMessagesCount;

            var filter = new LogFilter();
            var processing = new LogProcessingService(session, filter);
            // Must run on the UI thread; a static ctor would capture a null context.
            var adapter = new CoreToUiAdapter(SynchronizationContext.Current, processing);
            var importService = new LogImportService(session);
            var udpFactory = new UdpSourceFactory(settings);
            var query = new LogQueryService();
            var loggerFilter = new LoggerFilterState();
            var viewState = new LogViewState();
            var coordinator = new FilterCoordinator(processing, loggerFilter);
            var projector = new LogEntryProjector(settings.Receivers, settings);
            var fileWatch = new LogFileWatchService(processing);
            var treeBuilder = new LoggerTreeBuilder();
            var treeMarker = new LoggerTreeMarker(viewState, settings);

            return new LogViewModelDependencies
            {
                Settings = settings,
                Dialogs = dialogs,
                Files = files,
                Clipboard = clipboard,
                TimelineTimer = timer,
                Session = session,
                Processing = processing,
                Adapter = adapter,
                ImportService = importService,
                UdpFactory = udpFactory,
                Query = query,
                ViewState = viewState,
                Coordinator = coordinator,
                Projector = projector,
                FileWatch = fileWatch,
                TreeBuilder = treeBuilder,
                TreeMarker = treeMarker
            };
        }
    }
}
