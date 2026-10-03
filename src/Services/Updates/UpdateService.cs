using System;
using System.IO;
using System.Reflection;
using System.Windows;
using AutoUpdaterDotNET;
using NLog;

namespace LogViewer.Services.Updates
{
    /// <summary>
    /// AutoUpdater.NET over a manifest in GitHub Releases. The library reads the manifest, downloads the zip,
    /// verifies its checksum and starts ZipExtractor; the dialog and skip / remind-later are ours.
    /// </summary>
    public sealed class UpdateService : IUpdateService
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// GitHub redirects <c>latest/download/NAME</c> to the asset of the latest published release; drafts and
        /// pre-releases are not "latest", and release.yml attaches update.xml to stable releases only.
        /// </summary>
        private const string DefaultManifestUrl = "https://github.com/Styort/LogViewer/releases/latest/download/update.xml";

#if DEBUG
        /// <summary>
        /// Debug builds only: points the updater at a local manifest (http://localhost/... or file:///...)
        /// to test the whole update flow without publishing a release. Release builds ignore it.
        /// </summary>
        private const string ManifestOverrideVariable = "LOGVIEWER_UPDATE_MANIFEST";

        private static string ManifestUrl
        {
            get
            {
                var overrideUrl = Environment.GetEnvironmentVariable(ManifestOverrideVariable);
                return string.IsNullOrWhiteSpace(overrideUrl) ? DefaultManifestUrl : overrideUrl;
            }
        }
#else
        private const string ManifestUrl = DefaultManifestUrl;
#endif

        private readonly UpdateCheckHandler _handler;
        private bool _isManualCheck;

        public UpdateService(string settingsDirectory, IDialogService dialogs)
        {
            var installer = new WpfUpdateInstaller();
            _handler = new UpdateCheckHandler(
                new WpfUpdatePrompt(),
                dialogs,
                installer,
                new JsonUpdateStateStore(Path.Combine(settingsDirectory, "update_state.json")));

            AutoUpdater.InstalledVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            // settings.xml and logs/ may live next to the exe; the zip only overwrites the files it contains.
            AutoUpdater.ClearAppDirectory = false;
            // runas on every update would show UAC for a portable copy in a user folder.
            AutoUpdater.RunUpdateAsAdmin = !AppDirectoryAccess.IsWritable(AppDomain.CurrentDomain.BaseDirectory);
            AutoUpdater.PersistenceProvider = new NoOpPersistenceProvider();
            AutoUpdater.CheckForUpdateEvent += OnCheckForUpdate;
            AutoUpdater.ApplicationExitEvent += installer.ExitForUpdate;

            logger.Debug($"Updates configured: installed {AutoUpdater.InstalledVersion}, run as admin {AutoUpdater.RunUpdateAsAdmin}, manifest {ManifestUrl}");
        }

        public void CheckInBackground() => Start(false);

        public void CheckManually() => Start(true);

        private void Start(bool manual)
        {
            _isManualCheck = manual;
            AutoUpdater.ReportErrors = manual;
            AutoUpdater.Start(ManifestUrl);
        }

        private void OnCheckForUpdate(UpdateInfoEventArgs args)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => OnCheckForUpdate(args));
                return;
            }

            try
            {
                _handler.Handle(new UpdateCheckResult
                {
                    Error = args.Error,
                    IsUpdateAvailable = args.IsUpdateAvailable,
                    InstalledVersion = args.InstalledVersion,
                    AvailableVersion = args.CurrentVersion,
                    ChangelogUrl = args.ChangelogURL,
                    Source = args
                }, _isManualCheck);
            }
            catch (Exception e)
            {
                // Raised from the library's BackgroundWorker completion; an exception there would be lost.
                logger.Error(e, "Update handling failed");
            }
        }
    }
}
