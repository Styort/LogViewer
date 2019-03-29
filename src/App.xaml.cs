using System;
using System.ComponentModel;
using System.Deployment.Application;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Xml.Serialization;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.Views;
using NLog;

namespace LogViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LogViewer", "settings.xml");

        public ResourceDictionary ThemeDictionary => Resources.MergedDictionaries[1];

        public void ChangeTheme(Uri uri)
        {
            ThemeDictionary.MergedDictionaries[1] = new ResourceDictionary { Source = uri };
        }

        static Mutex mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
        [STAThread]
        protected override void OnStartup(StartupEventArgs e)
        {
            logger.Trace("OnStartup");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            var ser = new XmlSerializer(Settings.Instance.GetType());
            if (File.Exists(settingsPath))
            {
                try
                {
                    using (var fs = new FileStream(settingsPath, FileMode.Open))
                    {
                        Settings settings = (Settings)ser.Deserialize(fs);
                        Settings.Instance.AutoStartInStartup = settings.AutoStartInStartup;
                        Settings.Instance.MinimizeToTray = settings.MinimizeToTray;
                        Settings.Instance.CurrentTheme = settings.CurrentTheme;
                        Settings.Instance.DataFormat = settings.DataFormat;
                        Settings.Instance.IgnoredIPs = settings.IgnoredIPs;
                        Settings.Instance.Receivers = settings.Receivers;
                        Settings.Instance.FontColor = settings.FontColor;
                        Settings.Instance.OnlyOneAppInstance = settings.OnlyOneAppInstance;
                        Settings.Instance.IsEnabledMaxMessageBufferSize = settings.IsEnabledMaxMessageBufferSize;
                        Settings.Instance.MaxMessageBufferSize = settings.MaxMessageBufferSize;
                        Settings.Instance.DeletedMessagesCount = settings.DeletedMessagesCount;
                        Settings.Instance.IsShowSourceColumn = settings.IsShowSourceColumn;
                        Settings.Instance.IsShowThreadColumn = settings.IsShowThreadColumn;
                        Settings.Instance.IsShowTaskbarProgress = settings.IsShowTaskbarProgress;
                        Settings.Instance.Language = settings.Language;
                    }

                    Settings.Instance.ApplyTheme();
                    Settings.Instance.ApplyLanguage(Settings.Instance.Language);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "An error occurred while read and apply settings.");
                }
            }

            if (Settings.Instance.OnlyOneAppInstance)
            {
                try
                {
                    if (mutex.WaitOne(TimeSpan.Zero, true))
                    {
                        mutex.ReleaseMutex();
                    }
                    else
                    {
                        logger.Trace("OnStartup: one instance of Log Viewer already started");
                        MessageBox.Show(Locals.OnlyOneInstanceCanBeStartedMessageBoxText);
                        Environment.Exit(0);
                    }
                }
                catch (Exception exception)
                {
                    logger.Warn(exception, "OnStartup check mutext exception");
                }
            }

            base.OnStartup(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Fatal($"Unhandled exception {e.ExceptionObject as Exception}");
            Environment.Exit(1);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Fatal($"Unhandled Exception: {e.Exception}");
            Environment.Exit(2);
        }
    }
}
