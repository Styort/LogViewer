using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Views
{
    public partial class LoggerStatisticsWindow : Window
    {
        public event EventHandler<LogMessage> ShowLogEvent;

        public LoggerStatisticsWindow(LoggerStatisticsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.ShowLogEvent += (sender, message) => OnShowLogEvent(message);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            if (DataContext is LoggerStatisticsViewModel vm)
                vm.Refresh();
            ApplyDefaultSort();
        }

        private void ApplyDefaultSort()
        {
            var dataView = CollectionViewSource.GetDefaultView(StatisticsListView.ItemsSource);
            if (dataView == null)
                return;
            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(nameof(LoggerStatItem.Total), ListSortDirection.Descending));
            dataView.Refresh();
        }

        #region Сортировка по нажатию на заголовок таблицы

        private GridViewColumnHeader lastHeaderClicked;
        private ListSortDirection lastDirection = ListSortDirection.Descending;

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            if (headerClicked == null || headerClicked.Role == GridViewColumnHeaderRole.Padding)
                return;

            ListSortDirection direction;
            if (headerClicked != lastHeaderClicked)
                direction = ListSortDirection.Ascending;
            else
                direction = lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
            var sortBy = columnBinding?.Path.Path;
            if (string.IsNullOrEmpty(sortBy))
                sortBy = nameof(LoggerStatItem.Total);

            Sort(sortBy, direction);

            lastHeaderClicked = headerClicked;
            lastDirection = direction;
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            var dataView = CollectionViewSource.GetDefaultView(StatisticsListView.ItemsSource);
            if (dataView == null)
                return;

            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            dataView.Refresh();
        }

        #endregion

        protected virtual void OnShowLogEvent(LogMessage e)
        {
            ShowLogEvent?.Invoke(this, e);
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item && item.Content is LoggerStatItem stat)
            {
                if (DataContext is LoggerStatisticsViewModel vm)
                    vm.ShowItem(stat);
            }
        }
    }
}
