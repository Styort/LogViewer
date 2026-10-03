using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LogViewer.MVVM.Models;

namespace LogViewer.Helpers
{
    /// <summary>
    /// Keeps what the user is looking at in a virtualized log <see cref="ListView"/> when a filter replaces
    /// its <see cref="ItemsControl.ItemsSource"/>.
    /// </summary>
    /// <remarks>
    /// A new ItemsSource makes WPF clear the selection and scroll to the first row. The keeper remembers
    /// the selection and the top visible row while the user works, and after the replacement:
    /// <list type="bullet">
    /// <item>selects again every previously selected row that is still in the list;</item>
    /// <item>if the selected row survived, keeps it on the same screen row, or centers it if it was off screen;</item>
    /// <item>otherwise keeps the old top row, or the first row after it in buffer order if it was filtered out;</item>
    /// <item>in follow mode scrolls to the last row instead.</item>
    /// </list>
    /// Requires <c>ScrollViewer.CanContentScroll="True"</c> with a VirtualizingStackPanel, so that the
    /// vertical offset is an item index.
    /// <para>
    /// With a GridView the ListView contains two ScrollViewers: the rows one (templated parent is the
    /// ListView) and a header one nested in it. Both raise ScrollChanged through the ListView; only the
    /// rows ScrollViewer is tracked and scrolled.
    /// </para>
    /// </remarks>
    public sealed class ListViewViewKeeper
    {
        private readonly ListView _listView;
        private readonly Func<bool> _isFollowingTail;
        private ScrollViewer _scrollViewer;
        private object _currentSource;
        private LogMessage _topItem;
        private List<object> _selectedItems = new List<object>();
        // Screen row of the primary selected item relative to the top row; -1 when it is off screen.
        private int _selectedScreenRow = -1;
        private int _restoreVersion;

        public ListViewViewKeeper(ListView listView, Func<bool> isFollowingTail)
        {
            _listView = listView ?? throw new ArgumentNullException(nameof(listView));
            _isFollowingTail = isFollowingTail ?? (() => false);
            _currentSource = listView.ItemsSource;

            _listView.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
            _listView.SelectionChanged += OnSelectionChanged;
            // Lives as long as the ListView, so the strong reference held by AddValueChanged is harmless.
            DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListView))
                .AddValueChanged(_listView, OnItemsSourceChanged);
        }

        /// <summary>
        /// True while the view is being restored after an ItemsSource change. Selection and scroll events
        /// raised in that window come from the keeper or from WPF, not from the user.
        /// </summary>
        public bool IsRestoring { get; private set; }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (IsRestoring || !ReferenceEquals(e.OriginalSource, GetScrollViewer()))
                return;
            RememberView();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // While ItemsSource is being swapped, WPF clears the selection. That event can arrive before
            // OnItemsSourceChanged (the source already differs) or during the restore; neither is the user.
            if (IsRestoring || !ReferenceEquals(_listView.ItemsSource, _currentSource))
                return;
            _selectedItems = _listView.SelectedItems.Cast<object>().ToList();
            RememberView();
        }

        private void RememberView()
        {
            var items = _listView.Items;
            int top = TopIndex();
            _topItem = top >= 0 && top < items.Count ? items[top] as LogMessage : null;

            _selectedScreenRow = -1;
            var selected = _listView.SelectedItem;
            if (selected == null || top < 0)
                return;
            // Only the visible rows are checked: IndexOf over a million rows on every scroll is too slow.
            int visible = (int)Math.Ceiling(GetScrollViewer()?.ViewportHeight ?? 0) + 1;
            for (int i = top; i < items.Count && i < top + visible; i++)
            {
                if (ReferenceEquals(items[i], selected))
                {
                    _selectedScreenRow = i - top;
                    return;
                }
            }
        }

        private void OnItemsSourceChanged(object sender, EventArgs e)
        {
            _currentSource = _listView.ItemsSource;
            IsRestoring = true;
            int version = ++_restoreVersion;
            var selected = _selectedItems;
            var primary = selected.FirstOrDefault();
            int screenRow = _selectedScreenRow;
            var topItem = _topItem;

            // Loaded runs after the layout pass for the new items, so offsets and containers are valid.
            _listView.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (version != _restoreVersion)
                    return;
                try
                {
                    Restore(selected, primary, screenRow, topItem);
                }
                finally
                {
                    // Background runs after the layout pass caused by the restore itself.
                    _listView.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (version == _restoreVersion)
                            IsRestoring = false;
                    }), DispatcherPriority.Background);
                }
            }), DispatcherPriority.Loaded);
        }

        private void Restore(List<object> selected, object primary, int screenRow, LogMessage topItem)
        {
            var items = _listView.Items;
            var survivors = FindSurvivors(items, selected);
            bool primarySurvived = primary != null && survivors.Count > 0 && ReferenceEquals(survivors[0], primary);

            if (survivors.Count > 0)
            {
                _listView.SelectedItem = survivors[0];
                for (int i = 1; i < survivors.Count; i++)
                    _listView.SelectedItems.Add(survivors[i]);
                _selectedItems = survivors;
            }
            // When nothing survived, the remembered selection is kept: a later filter that brings the
            // rows back (for example, lowering the level again) selects them again.

            if (items.Count == 0)
                return;

            var scrollViewer = GetScrollViewer();
            if (_isFollowingTail())
            {
                _listView.ScrollIntoView(items[items.Count - 1]);
            }
            else if (scrollViewer != null)
            {
                int top = -1;
                int selectedRow = -1;
                if (primarySurvived)
                {
                    // The selected row stays where the user saw it; if it was scrolled away (or came back
                    // after being filtered out), it is brought to the middle of the viewport.
                    int viewportRows = Math.Max(1, (int)scrollViewer.ViewportHeight);
                    selectedRow = screenRow >= 0 && screenRow < viewportRows ? screenRow : viewportRows / 2;
                    top = Math.Max(0, items.IndexOf(primary) - selectedRow);
                }
                if (top < 0)
                    top = FindTopIndex(items, topItem);
                if (top >= 0)
                {
                    scrollViewer.ScrollToVerticalOffset(top);
                    _topItem = items[top] as LogMessage;
                    _selectedScreenRow = selectedRow;
                }
            }

            // Keep keyboard navigation on the selected row, but never steal focus from the control the user
            // is typing in (search box, level combo).
            if (primarySurvived && _listView.IsKeyboardFocusWithin)
            {
                _listView.UpdateLayout();
                (_listView.ItemContainerGenerator.ContainerFromItem(primary) as ListViewItem)?.Focus();
            }
        }

        private static List<object> FindSurvivors(ItemCollection items, List<object> selected)
        {
            if (selected.Count == 0)
                return new List<object>();
            if (selected.Count <= 8)
                return selected.Where(items.Contains).ToList();
            // Ctrl+A over a large list: one pass over the items instead of IndexOf per selected row.
            var present = new HashSet<object>(items.Cast<object>());
            return selected.Where(present.Contains).ToList();
        }

        private static int FindTopIndex(ItemCollection items, LogMessage topItem)
        {
            if (topItem == null)
                return -1;
            int index = items.IndexOf(topItem);
            if (index >= 0)
                return index;

            // The top row was filtered out: continue from the first row after it in the buffer.
            // Only meaningful in buffer order, not after a column sort.
            if (items.SortDescriptions.Count > 0 || topItem.Sequence == 0)
                return -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is LogMessage message && message.Sequence >= topItem.Sequence)
                    return i;
            }
            return items.Count - 1;
        }

        private int TopIndex()
        {
            var scrollViewer = GetScrollViewer();
            return scrollViewer == null ? -1 : (int)scrollViewer.VerticalOffset;
        }

        private ScrollViewer GetScrollViewer()
        {
            // Re-resolved if the template was applied again (theme switch).
            if (_scrollViewer == null || !ReferenceEquals(_scrollViewer.TemplatedParent, _listView))
                _scrollViewer = FindRowsScrollViewer(_listView);
            return _scrollViewer;
        }

        private ScrollViewer FindRowsScrollViewer(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer viewer && ReferenceEquals(viewer.TemplatedParent, _listView))
                    return viewer;
                var nested = FindRowsScrollViewer(child);
                if (nested != null)
                    return nested;
            }
            return null;
        }
    }
}
