using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Views
{
    public partial class BookmarkListWindow : Window
    {
        private GridViewColumnHeader lastHeaderClicked;
        private ListSortDirection lastDirection = ListSortDirection.Ascending;

        public BookmarkListWindow(BookmarkListViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            ApplyDefaultSort();
        }

        private void ApplyDefaultSort()
        {
            var dataView = CollectionViewSource.GetDefaultView(BookmarksListView.ItemsSource);
            if (dataView == null)
                return;
            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(nameof(LogBookmark.Time), ListSortDirection.Ascending));
            dataView.Refresh();
        }

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            if (headerClicked == null || headerClicked.Role == GridViewColumnHeaderRole.Padding)
                return;

            var direction = headerClicked != lastHeaderClicked
                ? ListSortDirection.Ascending
                : lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
            var sortBy = columnBinding?.Path.Path ?? nameof(LogBookmark.Time);
            Sort(sortBy, direction);

            lastHeaderClicked = headerClicked;
            lastDirection = direction;
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            var dataView = CollectionViewSource.GetDefaultView(BookmarksListView.ItemsSource);
            if (dataView == null)
                return;

            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            dataView.Refresh();
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item && item.Content is LogBookmark bookmark
                && DataContext is BookmarkListViewModel vm)
            {
                vm.GoToItem(bookmark);
            }
        }
    }
}
