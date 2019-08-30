using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Application;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using LogViewer.Localization;
using LogViewer.MVVM.Views;
using NLog;

namespace LogViewer
{
    /// <summary>
    /// Управляет загрузкой и установкой обновлений
    /// </summary>
    public static class UpdateManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const int CHECK_UPDATE_PERIOD = 600000;
        private static Timer updateTimer;
        private static ApplicationDeployment applicationDeployment;

        /// <summary>
        /// Запускает проверку наличия обновлений
        /// </summary>
        public static void StartCheckUpdate()
        {
            logger.Debug("StartCheckUpdate");

            updateTimer = new Timer(UpdaterPeriodicProcess, null, 0, CHECK_UPDATE_PERIOD);
        }

        /// <summary>
        /// Останавливает проверку на наличие обновлений
        /// </summary>
        public static void StopCheckUpdate()
        {
            logger.Debug("StopCheckUpdate");
            updateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Проверка наличия новых обновлений
        /// </summary>
        /// <returns></returns>
        public static bool CheckForUpdates()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                UpdateCheckInfo info = applicationDeployment.CheckForDetailedUpdate();
                return info.UpdateAvailable;
            }

            return false;
        }

        /// <summary>
        /// Установить новое обновление
        /// </summary>
        public static void InstallNewUpdate()
        {
            try
            {
                UpdateCheckInfo info = applicationDeployment.CheckForDetailedUpdate();
                if (info.UpdateAvailable)
                {
                    applicationDeployment.UpdateCompleted += OnUpdateCompleted;
                    logger.Debug($"New update available: {info.AvailableVersion}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (ShowNewUpdateDialog(info))
                        {
                            logger.Debug("Start update async");
                            applicationDeployment.UpdateAsync();
                        }
                        else
                        {
                            logger.Debug("Update scheduled at next launch");
                            StopCheckUpdate();
                            applicationDeployment.UpdateCompleted -= OnUpdateCompleted;
                        }
                    });
                }
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while install new new update");
            }
        }

        /// <summary>
        /// Показывает окно с информацией по обновлению
        /// </summary>
        /// <param name="info">Информация по обновлению</param>
        /// <returns></returns>
        private static bool ShowNewUpdateDialog(UpdateCheckInfo info)
        {
            NewUpdateAvailableDialog newUpdateAvailableDialog = new NewUpdateAvailableDialog(info);
            newUpdateAvailableDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            newUpdateAvailableDialog.Owner = Application.Current.MainWindow;
            newUpdateAvailableDialog.ShowDialog();
            return newUpdateAvailableDialog.DialogResult.HasValue && newUpdateAvailableDialog.DialogResult.Value;
        }

        /// <summary>
        /// Проверяет наличие обновлений
        /// </summary>
        public static void UpdaterPeriodicProcess(object state)
        {
            if (!CheckForUpdates()) return;
            InstallNewUpdate();
        }

        private static bool isUpdated;

        private static void OnUpdateCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (isUpdated) return;

            logger.Debug($"Update successfully installed with cancelled - {e.Cancelled}, error - {e.Error}, state - {e.UserState}");
            StopCheckUpdate();
            applicationDeployment.UpdateCompleted -= OnUpdateCompleted;
            isUpdated = true;

            if (e.Error != null)
            {
                logger.Warn($"Update completed with error: {e.Error.Message}");
                MessageBox.Show(Locals.Error, $"{Locals.UpdateInstalledErrorMessage}{Environment.NewLine}{e.Error.Message}",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (e.Cancelled)
            {
                logger.Debug("Update was cancelled");
                MessageBox.Show(Locals.UpdateWasCancelled);
                return;
            }

            MessageBoxResult dialogResult = MessageBox.Show(Locals.UpdateInstalledSuccessfully, Locals.SuccessfullyInstalled, MessageBoxButton.YesNo, MessageBoxImage.Question);
            logger.Debug($"Dialog result  = {dialogResult}");
            if (dialogResult != MessageBoxResult.Yes) return;
            RestartClickOnceApplication();
        }

        /// <summary>
        /// Выполняет перезагрузку приложения после успешного обновления
        /// </summary>
        private static void RestartClickOnceApplication()
        {
            logger.Debug("RestartClickOnceApplication");
            try
            {
                XDocument xDocument;
                using (MemoryStream memoryStream = new MemoryStream(AppDomain.CurrentDomain.ActivationContext.DeploymentManifestBytes))
                using (XmlTextReader xmlTextReader = new XmlTextReader(memoryStream))
                {
                    xDocument = XDocument.Load(xmlTextReader);
                }
                var description = xDocument.Root.Elements().First(p => p.Name.LocalName == "description");
                logger.Debug($"RestartClickOnceApplication: desctiption = {description}");
                var publisher = description.Attributes().First(a => a.Name.LocalName == "publisher");
                logger.Debug($"RestartClickOnceApplication: publisher = {publisher}");
                var product = description.Attributes().First(a => a.Name.LocalName == "product");
                logger.Debug($"RestartClickOnceApplication: product = {product}");

                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs",
                    publisher.Value, product.Value, product.Value + ".appref-ms");
                logger.Debug($"RestartClickOnceApplication: path = {path}");
                if (File.Exists(path))
                {
                    Process.Start(path);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "RestartClickOnceApplication exception");
            }
        }

        public static void Dispose()
        {
            updateTimer?.Dispose();
        }
    }
}
