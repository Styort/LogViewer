using System;
using System.ComponentModel;
using System.Deployment.Application;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Xml.Serialization;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.Views;
using Microsoft.Win32;
using NLog;

namespace LogViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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

            Settings.Instance.Load();

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

        //public static void SetAssociation(string Extension, string KeyName, string OpenWith, string FileDescription)
        //{

        //    var currentUser = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\" + Extension, true);
        //    if(currentUser == null) return;
        //    currentUser.DeleteSubKey("UserChoice", false);
        //    currentUser.Close();

        //    // Tell explorer the file association has been changed
        //    SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        //}

        //[DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
