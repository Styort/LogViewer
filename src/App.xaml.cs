using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Xml.Serialization;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using NLog;

namespace LogViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string SETTINGS_NAME = "settings.xml";

        public ResourceDictionary ThemeDictionary => Resources.MergedDictionaries[1];

        public void ChangeTheme(Uri uri)
        {
            ThemeDictionary.MergedDictionaries[1] = new ResourceDictionary{Source =  uri};
        }

        static Mutex mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
        [STAThread]
        protected override void OnStartup(StartupEventArgs e)
        {
            logger.Trace("OnStartup");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            var ser = new XmlSerializer(Settings.Instance.GetType());
            if (File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}{SETTINGS_NAME}"))
            {
                try
                {
                    using (var fs = new FileStream($"{AppDomain.CurrentDomain.BaseDirectory}{SETTINGS_NAME}", FileMode.Open))
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
                        Settings.Instance.IsShowIpColumn = settings.IsShowIpColumn;
                        Settings.Instance.IsShowThreadColumn = settings.IsShowThreadColumn;
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
