using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Application;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Navigation;
using System.Xml.Serialization;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.Views;
using LogViewer.MVVM.ViewModels.Log;
using Microsoft.Win32;
using NLog;

namespace LogViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static bool IsManualStartup { get; private set; } = false;
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
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            if (e.Args.Any(x => File.Exists(x) && (ArchiveLogExtractor.IsImportableFile(x) || SessionViewModel.IsSessionFile(x))))
            {
                IsManualStartup = true;

                try
                {
                    if (!mutex.WaitOne(TimeSpan.Zero, true))
                    {
                        var proc = Process.GetCurrentProcess();
                        var processName = proc.ProcessName.Replace(".vshost", "");
                        var runningProcess = Process.GetProcesses()
                            .FirstOrDefault(x => (x.ProcessName == processName || x.ProcessName == proc.ProcessName || x.ProcessName == proc.ProcessName + ".vshost") && x.Id != proc.Id);

                        if (runningProcess == null)
                        {
                            var app = new App();
                            app.InitializeComponent();
                            var window = new MainWindow();
                            MVVM.Views.MainWindow.HandleParameter(e.Args);
                            app.Run(window);
                            return; 
                        }

                        UnsafeNative.SendMessage(runningProcess.MainWindowHandle, string.Join(" ", e.Args));
                        
                        Environment.Exit(0);
                    }
                }
                catch (Exception exception)
                {
                    logger.Warn(exception, "OnStartup check mutext exception (open with arguments)");
                }
            }

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

            try
            {
                if (!IsAssociated())
                {
                    var filePath = Process.GetCurrentProcess().MainModule.FileName;

                    SetAssociation(".txt", "LogViewer", filePath);
                    SetAssociation(".log", "LogViewer", filePath);
                }
            }
            catch (Exception exception)
            {
                logger.Warn(exception, "OnStartup set file association exception");
            }

            base.OnStartup(e);

#if DEBUG
            // WPF does not validate bindings at compile time; without this a broken Path after the VM split stays silent.
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());
#endif

            Task.Run(() =>
            {
                // give the window time to load
                Thread.Sleep(5000);
                UpdateManager.StartCheckUpdate();
            });
        }

        #region Unhandled exceptions

        // More UI exceptions than this inside the window means the app is stuck in a failing loop
        // (a throwing binding, a render callback); showing a dialog for each would never end.
        private const int MaxUiErrorsInWindow = 5;
        private static readonly TimeSpan UiErrorWindow = TimeSpan.FromSeconds(10);
        private readonly Queue<DateTime> _recentUiErrors = new Queue<DateTime>();
        private bool _isShowingErrorDialog;

        /// <summary>Matches the NLog file target in App.config (<c>${basedir}/logs</c>).</summary>
        private static string AppLogDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        /// <summary>
        /// A non-UI thread crashed. The CLR terminates the process after this handler, so the only things
        /// left to do are to flush the log and tell the user why the window disappears.
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            logger.Fatal(exception, "Unhandled exception on a background thread");
            LogManager.Flush();
            TryShowError(string.Format(Locals.FatalErrorMessage, exception?.Message, AppLogDirectory), MessageBoxImage.Stop);
            Environment.Exit(1);
        }

        /// <summary>
        /// A UI command or binding threw. The buffer may hold hours of logs that exist nowhere else, so the
        /// error is shown and handled instead of closing the window; only a burst of repeated failures exits.
        /// </summary>
        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error(e.Exception, "Unhandled exception on the UI thread");
            e.Handled = true;

            var now = DateTime.UtcNow;
            _recentUiErrors.Enqueue(now);
            while (_recentUiErrors.Count > 0 && now - _recentUiErrors.Peek() > UiErrorWindow)
                _recentUiErrors.Dequeue();

            if (_recentUiErrors.Count > MaxUiErrorsInWindow)
            {
                logger.Fatal("Too many unhandled UI exceptions in a row, shutting down");
                LogManager.Flush();
                TryShowError(string.Format(Locals.FatalErrorMessage, e.Exception.Message, AppLogDirectory), MessageBoxImage.Stop);
                Environment.Exit(2);
                return;
            }

            // The modal dialog pumps messages; a second failure while it is open is only logged.
            if (_isShowingErrorDialog)
                return;

            _isShowingErrorDialog = true;
            try
            {
                TryShowError(string.Format(Locals.UnhandledErrorMessage, e.Exception.Message, AppLogDirectory), MessageBoxImage.Error);
            }
            finally
            {
                _isShowingErrorDialog = false;
            }
        }

        /// <summary>
        /// Fire-and-forget tasks (for example <c>var _ = RefreshAsync()</c>) lose their exceptions otherwise.
        /// On .NET 4.5+ an unobserved exception does not terminate the process, so logging is enough.
        /// </summary>
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        }

        private static void TryShowError(string message, MessageBoxImage icon)
        {
            try
            {
                MessageBox.Show(message, Locals.Error, MessageBoxButton.OK, icon);
            }
            catch (Exception dialogException)
            {
                // The error is already logged; a broken dispatcher must not hide the original failure.
                logger.Warn(dialogException, "Could not show the error dialog");
            }
        }

        #endregion

        #region File Assotiation

        private bool IsAssociated()
        {
            return Registry.CurrentUser.OpenSubKey(@"Software\Classes\LogViewer", false) != null;
        }

        [DllImport("Shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private const int SHCNE_ASSOCCHANGED = 0x8000000;
        private const int SHCNF_FLUSH = 0x1000;

        private void SetAssociation(string extension, string progId, string applicationFilePath)
        {
            bool madeChanges = false;

            madeChanges |= SetKeyDefaultValue($@"Software\Classes\{progId}\shell\open\command", "\"" + applicationFilePath + "\" \"%1\"");
            madeChanges |= SetProgIdValue($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids", progId);

            if (madeChanges) SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
        }

        private bool SetProgIdValue(string path, string progId)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(path))
            {
                if (key.GetValueNames().All(x => x != progId))
                {
                    key.SetValue(progId, Encoding.Unicode.GetBytes(string.Empty), RegistryValueKind.Binary);
                    return true;
                }
            }
            return false;
        }

        private bool SetKeyDefaultValue(string keyPath, string value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (key.GetValue(null) as string != value)
                {
                    key.SetValue(null, value);
                    return true;
                }
            }
            return false;
        }

        #endregion


        public void Dispose()
        {
            UpdateManager.Dispose();
        }
    }
}
