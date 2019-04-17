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
using System.Xml;
using System.Xml.Linq;
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
        }

        /// <summary>
        /// Происходит при загрзуке окна. 
        /// Если файл лога открыли через это приложение, то показывается сразу окно импорта.
        /// </summary>
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments?.ActivationData != null)
            {
                string[] activationData = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                foreach (var arg in activationData.Where(x => x.EndsWith(".txt") || x.EndsWith(".log")))
                {
                    ((LogViewModel)DataContext).IsManualStartup = true;
                    ((LogViewModel)DataContext).ImportLogs(arg);
                }
                return;
            }

            var args = Environment.GetCommandLineArgs().Where(x => x.EndsWith(".txt") || x.EndsWith(".log")).ToList();
            if (args.Any())
            {
                foreach (var arg in args)
                {
                    if (arg.EndsWith(".txt") || arg.EndsWith(".log"))
                    {
                        ((LogViewModel)DataContext).IsManualStartup = true;
                        ((LogViewModel)DataContext).ImportLogs(arg);
                    }
                }
                return;
            }

            DisplayChangeLog();
        }

        #region Переход в tray

        /// <summary>
        /// Происходит при изменении состояния окна. 
        /// Если стоит настройка перехода приложения в трей, то переход осуществляется тут.
        /// </summary>
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && Settings.Instance.MinimizeToTray)
            {
                if (trayIcon == null)
                {
                    trayIcon = new NotifyIcon
                    {
                        Icon = Properties.Resources.log,
                        Visible = true,
                        Text = "Log Viewer"
                    };
                    trayIcon.DoubleClick += delegate
                    {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                    };

                    trayIcon.ContextMenuStrip = new ContextMenuStrip();
                    ToolStripMenuItem openAppMenuItem = new ToolStripMenuItem("Open");
                    ToolStripMenuItem exitAppMenuItem = new ToolStripMenuItem("Exit");
                    // добавляем элементы в меню
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
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon.Icon = null;
            }

            base.OnClosed(e);
        }

        #endregion

        #region Скролл логов

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
            // переходим в начало логов
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
            // переходим в конец логов
            if (LogsListView.Items.Count > 0)
                LogsListView.ScrollIntoView(LogsListView.Items[LogsListView.Items.Count - 1]);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // если включен автоскролл и при этом человек нажал на какой-то элемент лога - выключаем автоскролл
            if (AutoScrollEnabled)
                AutoScrollEnabled = false;

            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // переходим к выбранному элементу (сделано для правильной работы поиска FindNext)
                    LogsListView.ScrollIntoView(LogsListView.SelectedItem);
                    LoggersTreeView.BringIntoView();
                });
            });
        }

        private void LogsListView_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // если включен автоскролл и произошла прокрутка наверх - то выключаем автоскролл
            if (AutoScrollEnabled && LogsListView.Items.Count > 0 && e.VerticalChange < 0)
                AutoScrollEnabled = false;

            // автоскролл 
            if (AutoScrollEnabled && LogsListView.Items.Count > 0)
                LogsListView.ScrollIntoView(LogsListView.Items[LogsListView.Items.Count - 1]);
        }

        #endregion

        #region Нажатие на чекбоксы дерева логгеров

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

        #region Сортировка по нажатию на заголовок таблицы

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
                x => Path.GetExtension(x) == ".txt" ||
                     Path.GetExtension(x) == ".log");

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
                    var file = files.FirstOrDefault(x => Path.GetExtension(x) == ".txt" ||
                                                         Path.GetExtension(x) == ".log");
                    if (file != null)
                    {
                        ((LogViewModel)this.DataContext).ImportLogs(file);
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

        public void Dispose()
        {
            trayIcon?.Dispose();
        }
    }
}