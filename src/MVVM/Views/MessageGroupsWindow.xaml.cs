using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;

namespace LogViewer.MVVM.Views
{
    public partial class MessageGroupsWindow : Window
    {
        public event EventHandler<LogMessage> ShowLogEvent;

        public MessageGroupsWindow(MessageGroupsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.ShowLogEvent += (sender, message) => OnShowLogEvent(message);
            Closed += (sender, args) => viewModel.OnWindowClosed();
        }

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
                direction = lastDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

            var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
            var sortBy = columnBinding?.Path.Path;
            if (string.IsNullOrEmpty(sortBy))
                sortBy = headerClicked.Column.CellTemplate != null
                    ? nameof(MessageGroupItem.DisplayText)
                    : nameof(MessageGroupItem.Count);

            var dataView = CollectionViewSource.GetDefaultView(GroupsListView.ItemsSource);
            if (dataView == null)
                return;
            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            dataView.Refresh();

            lastHeaderClicked = headerClicked;
            lastDirection = direction;
        }

        protected virtual void OnShowLogEvent(LogMessage e)
        {
            ShowLogEvent?.Invoke(this, e);
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item && item.Content is MessageGroupItem group)
            {
                if (DataContext is MessageGroupsViewModel vm)
                    vm.ShowItemLast(group);
            }
        }
    }
}
