using System;
using System.Windows;
using AutoUpdaterDotNET;
using NLog;

namespace LogViewer.Services.Updates
{
    internal sealed class WpfUpdateInstaller : IUpdateInstaller
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public bool Download(UpdateCheckResult update)
        {
            return update?.Source != null && AutoUpdater.DownloadUpdate(update.Source);
        }

        /// <summary>
        /// ZipExtractor waits until every LogViewer.exe process is gone. Minimize-to-tray, receiver threads and
        /// the single-instance mutex must not keep this one alive, so after a graceful close the process is exited.
        /// </summary>
        public void ExitForUpdate()
        {
            logger.Info("Exiting so ZipExtractor can replace the files");
            try
            {
                var app = Application.Current;
                app?.Dispatcher.Invoke(() =>
                {
                    // Closing the main window disposes the tray icon, which would otherwise linger in the notification area.
                    app.MainWindow?.Close();
                    app.Shutdown();
                });
            }
            catch (Exception e)
            {
                logger.Warn(e, "Graceful shutdown before the update failed");
            }

            LogManager.Flush();
            Environment.Exit(0);
        }
    }
}
