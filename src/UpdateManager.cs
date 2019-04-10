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
    public class UpdateManager : IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const int CHECK_UPDATE_PERIOD = 600000;
        private Timer updateTimer;
        
        /// <summary>
        /// Запускает проверку наличия обновлений
        /// </summary>
        public void StarCheckUpdate()
        {
            logger.Debug("StarCheckUpdate");

            updateTimer = new Timer(CheckUpdate, null, 0, CHECK_UPDATE_PERIOD);
        }

        /// <summary>
        /// Останавливает проверку на наличие обновлений
        /// </summary>
        public void StopCheckUpdate()
        {
            logger.Debug("StopCheckUpdate");
            updateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Проверяет наличие обновлений
        /// </summary>
        private void CheckUpdate(object state)
        {
            try
            {
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
                    ad.UpdateCompleted += OnUpdateCompleted;
                    UpdateCheckInfo info = ad.CheckForDetailedUpdate();
                    if (info.UpdateAvailable)
                    {
                        logger.Debug($"New update available: {info.AvailableVersion}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            NewUpdateAvailableDialog newUpdateAvailableDialog = new NewUpdateAvailableDialog(info);
                            newUpdateAvailableDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            newUpdateAvailableDialog.Owner = Application.Current.MainWindow;
                            newUpdateAvailableDialog.ShowDialog();

                            if (newUpdateAvailableDialog.DialogResult.HasValue &&
                                newUpdateAvailableDialog.DialogResult.Value)
                            {
                                logger.Debug("Start update async");
                                ad.UpdateAsync();
                            }
                            else
                            {
                                logger.Debug("Update scheduled at next launch");
                                StopCheckUpdate();
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "An error occurred while check for new updates");
            }
        }

        private bool isUpdated;

        private void OnUpdateCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (isUpdated) return;

            logger.Debug($"Update successfully intalled with cancelled - {e.Cancelled}, error - {e.Error}, state - {e.UserState}");
            StopCheckUpdate();
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
        private void RestartClickOnceApplication()
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

        public void Dispose()
        {
            updateTimer?.Dispose();
        }
    }
}
