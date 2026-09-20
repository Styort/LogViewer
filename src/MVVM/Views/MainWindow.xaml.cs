using System;
using System.ComponentModel;
using System.Deployment.Application;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml;
using System.Xml.Linq;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.TreeView;
using LogViewer.MVVM.ViewModels;
using NLog;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using Timer = System.Threading.Timer;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private NotifyIcon trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            AutoScrollButton.ToolTip = Locals.EnableAutoScroll;

            this.Loaded += (s, e) =>
            {
                MainWindow.WindowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                HwndSource.FromHwnd(MainWindow.WindowHandle)?.AddHook(HandleMessages);
            };
        }

        /// <summary>
        /// Runs when the window is loaded. 
        /// If a log file was opened with this app, import starts immediately.
        /// </summary>
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments?.ActivationData != null)
            {
                string[] activationData = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                var files = activationData.Where(x => ArchiveLogExtractor.IsImportableFile(x) && File.Exists(x)).ToList();
                if (files.Any())
                    ((LogViewModel)DataContext).ImportLogs(files);
                return;
            }

            var args = Environment.GetCommandLineArgs().Where(x => ArchiveLogExtractor.IsImportableFile(x) && File.Exists(x)).ToList();
            if (args.Any())
            {
                ((LogViewModel)DataContext).ImportLogs(args);
                return;
            }

            DisplayChangeLog();
        }

        #region Minimize to tray

        /// <summary>
        /// Runs when the window state changes. 
        /// If minimize-to-tray is enabled, the window is hidden here.
        /// </summary>
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && Settings.Instance.MinimizeToTray)
            {
                if (trayIcon == null)
                {
                    trayIcon = new NotifyIcon
                    {
                        Icon = Properties.Resources.log1,
                        Visible = true,
                        Text = "Log Viewer"
                    };
                    LogViewer.Services.Wpf.WpfAlertEffects.SharedTrayIcon = trayIcon;
                    trayIcon.DoubleClick += delegate
                    {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                    };

                    trayIcon.ContextMenuStrip = new ContextMenuStrip();
                    ToolStripMenuItem openAppMenuItem = new ToolStripMenuItem("Open");
                    ToolStripMenuItem exitAppMenuItem = new ToolStripMenuItem("Exit");
                    // add items to the tray menu
                    trayIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[] { openAppMenuItem, exitAppMenuItem });
                    trayIcon.ContextMenuStrip.ItemClicked += TrayIconContextMenuClick;
                }
                this.Hide();
            }

            base.OnStateChanged(e);
        }

        private void TrayIconContextMenuClick(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Text)
            {
                case "Open":
                    Show();
                    WindowState = WindowState.Normal;
                    break;
                case "Exit":
                    Close();
                    Environment.Exit(0);
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (trayIcon != null)
            {
                if (ReferenceEquals(LogViewer.Services.Wpf.WpfAlertEffects.SharedTrayIcon, trayIcon))
                    LogViewer.Services.Wpf.WpfAlertEffects.SharedTrayIcon = null;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon.Icon = null;
            }

            base.OnClosed(e);
        }

        #endregion

        #region Log list scrolling

        private bool autoScrollEnabled = false;

        protected bool AutoScrollEnabled
        {
            get => autoScrollEnabled;
            set
            {
                autoScrollEnabled = value;
                if (autoScrollEnabled)
                {
                    AutoScrollButton.Opacity = 0.5;
                    AutoScrollButton.ToolTip = Locals.DisableAutoScroll;
                }
                else
                {
                    AutoScrollButton.ToolTip = Locals.EnableAutoScroll;
                    AutoScrollButton.Opacity = 1;
                }
            }
        }

        private void OnScrollToTopButtonClick(object sender, RoutedEventArgs e)
        {
            // scroll to the first log
            if (LogsListView.Items.Count > 0)
                LogsListView.ScrollIntoView(LogsListView.Items[0]);
        }

        private void OnAutoScrollToBottomButtonClick(object sender, RoutedEventArgs e)
        {
            if (AutoScrollEnabled)
                AutoScrollEnabled = false;
            else
            {
                if (LogsListView.Items.Count > 0)
                    LogsListView.ScrollIntoView(LogsListView.Items[LogsListView.Items.Count - 1]);

                AutoScrollEnabled = true;
            }
        }

        private void OnScrollToBottomButtonClick(object sender, RoutedEventArgs e)
        {
            // scroll to the last log
            if (LogsListView.Items.Count > 0)
                LogsListView.ScrollIntoView(LogsListView.Items[LogsListView.Items.Count - 1]);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // if autoscroll is on and the user clicked a log row, turn autoscroll off
            if (AutoScrollEnabled)
                AutoScrollEnabled = false;

            if (DataContext is LogViewModel viewModel)
            {
                viewModel.SelectedLogs = LogsListView.SelectedItems.Cast<LogMessage>().ToList();
            }

            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // scroll the selected item into view (needed for Find Next)
                    LogsListView.ScrollIntoView(LogsListView.SelectedItem);
                    LoggersTreeView.BringIntoView();
                });
            });
        }

        private void LogsListView_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // List growth (a batch of Add) changes the offset; that is not the user scrolling up.
            if (AutoScrollEnabled && LogsListView.Items.Count > 0 && e.VerticalChange < 0 && e.ExtentHeightChange == 0)
                AutoScrollEnabled = false;

            // autoscroll to the last row (after a batch, Layout usually runs once, not N jumps from the background)
            if (AutoScrollEnabled && LogsListView.Items.Count > 0)
                LogsListView.ScrollIntoView(LogsListView.Items[LogsListView.Items.Count - 1]);
        }

        #endregion

        #region Logger tree checkbox clicks

        private void TreeViewCheckBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CheckBox currentCheckBox = (CheckBox)sender;
            CheckBoxId.CurrentСheckBoxId = currentCheckBox.Uid;
        }

        private void TreeViewCheckBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                CheckBox currentCheckBox = (CheckBox)sender;
                CheckBoxId.CurrentСheckBoxId = currentCheckBox.Uid;
            }
        }

        #endregion

        #region Sort on column header click

        GridViewColumnHeader lastHeaderClicked = null;
        ListSortDirection lastDirection = ListSortDirection.Ascending;

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;

            if (headerClicked != null && (string)headerClicked.Content == "Message")
                return;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    ListSortDirection direction;
                    if (headerClicked != lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        direction = lastDirection == ListSortDirection.Ascending
                            ? ListSortDirection.Descending
                            : ListSortDirection.Ascending;
                    }

                    var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate =
                            Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate =
                            Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header  
                    if (lastHeaderClicked != null && lastHeaderClicked != headerClicked)
                    {
                        lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    lastHeaderClicked = headerClicked;
                    lastDirection = direction;
                }
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
                CollectionViewSource.GetDefaultView(LogsListView.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        #endregion

        #region Drag and Drop

        private void LogsListView_OnDragOver(object sender, DragEventArgs e)
        {
            var file = ((string[])e.Data.GetData(DataFormats.FileDrop))?.FirstOrDefault(
                x => ArchiveLogExtractor.IsImportableFile(x));

            e.Effects = file != null ? DragDropEffects.Copy : DragDropEffects.None;

            e.Handled = true;
        }

        private void LogsListView_OnDrop(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any())
                {
                    var logFiles = files.Where(x => ArchiveLogExtractor.IsImportableFile(x));
                    if (logFiles.Any())
                    {
                        ((LogViewModel)this.DataContext).ImportLogs(logFiles);
                    }
                }
            }
        }

        #endregion

        private void DisplayChangeLog()
        {
            if (!ApplicationDeployment.IsNetworkDeployed)
                return;

            if (!ApplicationDeployment.CurrentDeployment.IsFirstRun)
                return;

            ReleaseNotesDialog releaseNotesDialog = new ReleaseNotesDialog();
            releaseNotesDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            releaseNotesDialog.Owner = this;
            releaseNotesDialog.ShowDialog();
        }

        #region Forward CLI args to an already running instance

        public static IntPtr WindowHandle { get; private set; }

        internal static void HandleParameter(string[] args)
        {
            if (Application.Current?.MainWindow is MainWindow mainWindow && args != null && args.Length > 0)
            {
                var files = args.Where(x => ArchiveLogExtractor.IsImportableFile(x) && File.Exists(x)).ToList();
                if (files.Any())
                    ((LogViewModel)mainWindow.DataContext).ImportLogs(files);
            }
        }

        private static IntPtr HandleMessages(IntPtr handle, int message, IntPtr wParameter, IntPtr lParameter, ref Boolean handled)
        {
            var data = UnsafeNative.GetMessage(message, lParameter);

            if (data != null)
            {
                if (Application.Current.MainWindow == null)
                    return IntPtr.Zero;

                if (Application.Current.MainWindow.WindowState == WindowState.Minimized)
                    Application.Current.MainWindow.WindowState = WindowState.Normal;

                UnsafeNative.SetForegroundWindow(new WindowInteropHelper
                    (Application.Current.MainWindow).Handle);

                var args = data.Split(' ');
                HandleParameter(args);
                handled = true;
            }

            return IntPtr.Zero;
        }

        #endregion

        public void Dispose()
        {
            trayIcon?.Dispose();
        }
    }
}