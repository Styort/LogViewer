using System;
using AutoUpdaterDotNET;

namespace LogViewer.Services.Updates
{
    /// <summary>
    /// Checks GitHub Releases for a newer build. Both calls must be made on the UI thread:
    /// AutoUpdater.NET reports back through a BackgroundWorker, which returns to the caller's context.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>Startup check: errors and "up to date" stay silent, skipped and postponed versions are not offered.</summary>
        void CheckInBackground();

        /// <summary>Settings button: reports errors and "up to date", offers even a skipped version.</summary>
        void CheckManually();
    }

    /// <summary>Outcome of one manifest check, decoupled from the library type so the handler can be tested.</summary>
    public sealed class UpdateCheckResult
    {
        /// <summary>Network or manifest failure; the other fields are meaningless when set.</summary>
        public Exception Error { get; set; }

        public bool IsUpdateAvailable { get; set; }

        public Version InstalledVersion { get; set; }

        /// <summary>Version from the manifest, <c>X.X.X.X</c>.</summary>
        public string AvailableVersion { get; set; }

        /// <summary>Release page on GitHub.</summary>
        public string ChangelogUrl { get; set; }

        /// <summary>Library arguments handed back to <see cref="AutoUpdater.DownloadUpdate"/>; null in tests.</summary>
        public UpdateInfoEventArgs Source { get; set; }
    }

    public enum UpdateChoice
    {
        /// <summary>The dialog was closed: nothing is remembered, the next start asks again.</summary>
        Dismissed,
        Update,
        RemindLater,
        Skip
    }

    /// <summary>The "new version available" dialog.</summary>
    public interface IUpdatePrompt
    {
        UpdateChoice Ask(UpdateCheckResult update);
    }

    /// <summary>Download, verification and hand-off to ZipExtractor.</summary>
    public interface IUpdateInstaller
    {
        /// <summary>
        /// Downloads the zip with progress and verifies the checksum. true means ZipExtractor has been started
        /// and is waiting for this process to exit; false means the download was cancelled or rejected.
        /// </summary>
        bool Download(UpdateCheckResult update);

        /// <summary>Terminates the process for real (tray and foreground threads included) so the files can be replaced.</summary>
        void ExitForUpdate();
    }

    /// <summary>"Skip this version" and "Remind me later" state.</summary>
    public interface IUpdateStateStore
    {
        Version SkippedVersion { get; set; }

        DateTime? RemindLaterAt { get; set; }
    }
}
