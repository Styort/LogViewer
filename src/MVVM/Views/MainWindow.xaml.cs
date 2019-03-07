using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.TreeView;
using LogViewer.MVVM.ViewModels;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using Path = System.IO.Path;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon trayIcon;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && Settings.Instance.MinimizeToTray)
            {
                if (Settings.Instance.MinimizeToTray)
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
                    AutoScrollButton.ToolTip = "Disable Auto Scroll to Last Log Message";
                }
                else
                {
                    AutoScrollButton.ToolTip = "Enable Auto Scroll to Last Log Message";
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
                App.Current.Dispatcher.Invoke(() =>
                {
                    // переходим к выбранному элементу (сделано для правильной работы поиска FindNext)
                    LogsListView.ScrollIntoView(LogsListView.SelectedItem);
                    LoggersTreeView.BringIntoView();
                });
            });
        }

        private void LogsListView_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // если включен автоскролл и человек при этом прокрутил вверх - то выключаем автоскролл
            if (AutoScrollEnabled && LogsListView.Items.Count > 0 && e.VerticalChange < 0)
                AutoScrollEnabled = false;

            // автоскролл 
            if (AutoScrollEnabled && LogsListView.Items.Count > 0)
                LogsListView.ScrollIntoView(LogsListView.Items[LogsListView.Items.Count - 1]);
        }


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
                        direction = lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
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

        private void LogsListView_OnDrop(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    // проверяем каждый закинутый файл и если подходит по расширению, то пробуем распарсить
                    var ext = Path.GetExtension(file);
                    if (ext != ".txt" && ext != ".log")
                        continue;
                    
                    ((LogViewModel)this.DataContext).ImportLogs(file);
                }
            }
        }
    }
}
