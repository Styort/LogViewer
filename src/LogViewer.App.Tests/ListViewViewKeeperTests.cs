using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    /// <summary>
    /// Real ListView in an off-screen window, bound like MainWindow (async ItemsSource, SelectedValue),
    /// so the order of WPF's own selection reset and the keeper's restore is exercised for real.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ListViewViewKeeperTests
    {
        private static readonly string[] ThemeDictionaries =
        {
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml",
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml",
            "pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.Indigo.xaml",
            "pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Accent/MaterialDesignColor.Lime.xaml"
        };

        static ListViewViewKeeperTests()
        {
            // Registers the pack:// scheme outside a running WPF Application.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        }

        private Window _window;
        private ListView _listView;
        private ListViewViewKeeper _keeper;
        private ListVm _vm;
        private List<LogMessage> _rows;
        private bool _following;

        [SetUp]
        public void SetUp()
        {
            _rows = Enumerable.Range(0, 1000)
                .Select(i => TestRows.Row("row " + i, sequence: i + 1))
                .ToList();
            _vm = new ListVm { Logs = _rows.ToList() };
            _following = false;

            // GridView like MainWindow: its template adds a second, header ScrollViewer whose ScrollChanged
            // bubbles through the ListView too and must not be mistaken for the rows' ScrollViewer.
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn { Header = "Message", DisplayMemberBinding = new Binding(nameof(LogMessage.Message)), Width = 300 });
            _listView = new ListView { View = gridView, SelectionMode = SelectionMode.Extended };
            ScrollViewer.SetCanContentScroll(_listView, true);
            VirtualizingPanel.SetIsVirtualizing(_listView, true);
            VirtualizingPanel.SetVirtualizationMode(_listView, VirtualizationMode.Recycling);
            _listView.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(ListVm.Logs)) { IsAsync = true });
            _listView.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(nameof(ListVm.SelectedLog)));
            _listView.DataContext = _vm;

            ScrollViewer.SetHorizontalScrollBarVisibility(_listView, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(_listView, ScrollBarVisibility.Auto);

            _window = new Window
            {
                Content = _listView,
                Width = 400,
                Height = 600,
                Left = -20000,
                Top = -20000,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            // Same theme dictionaries as App.xaml: MaterialDesign templates change the ScrollViewer layout.
            foreach (var source in ThemeDictionaries)
                _window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
            _window.Show();
            WaitUntil(() => ReferenceEquals(_listView.ItemsSource, _vm.Logs) && ScrollViewer != null);

            // Make the header ScrollViewer raise the first ScrollChanged the keeper sees, as it can in the
            // real window. The keeper used to adopt whichever ScrollViewer reported first.
            _keeper = new ListViewViewKeeper(_listView, () => _following);
            gridView.Columns[0].Width = 320;
            Pump();
        }

        [TearDown]
        public void TearDown()
        {
            _window.Close();
        }

        [Test]
        public void TopRowStillPresent_StaysAtTheTop()
        {
            ScrollTo(500);

            Replace(_rows.Where((r, i) => i % 2 == 0 || i == 500));

            Assert.That(TopItem(), Is.SameAs(_rows[500]));
        }

        [Test]
        public void TopRowFilteredOut_NextRowInBufferOrderGoesToTheTop()
        {
            ScrollTo(501);

            Replace(_rows.Where((r, i) => i % 2 == 0));

            Assert.That(TopItem(), Is.SameAs(_rows[502]));
        }

        [Test]
        public void SelectedRowSurvives_StaysSelectedOnTheSameScreenRow()
        {
            ScrollTo(500);
            Select(_rows[506]);

            Replace(_rows.Where((r, i) => i % 2 == 0));

            Assert.That(_listView.SelectedItem, Is.SameAs(_rows[506]));
            Assert.That(_vm.SelectedLog, Is.SameAs(_rows[506]), "the view model sees the selection again");
            int top = (int)ScrollViewer.VerticalOffset;
            Assert.That(_listView.Items.IndexOf(_rows[506]) - top, Is.EqualTo(6));
            Assert.That(_keeper.IsRestoring, Is.False);
        }

        [Test]
        public void MultiSelection_KeepsTheRowsThatSurvived()
        {
            ScrollTo(500);
            Select(_rows[504], _rows[506], _rows[507]);

            Replace(_rows.Where((r, i) => i % 2 == 0));

            Assert.That(_listView.SelectedItems.Cast<LogMessage>(), Is.EquivalentTo(new[] { _rows[504], _rows[506] }));
        }

        [Test]
        public void SelectedRowFilteredOutThenBack_IsSelectedAgain()
        {
            ScrollTo(500);
            Select(_rows[505]);

            Replace(_rows.Where((r, i) => i % 2 == 0));
            Assert.That(_listView.SelectedItem, Is.Null);
            Assert.That(TopItem(), Is.SameAs(_rows[500]));

            Replace(_rows);
            Assert.That(_listView.SelectedItem, Is.SameAs(_rows[505]));
        }

        [Test]
        public void SelectedRowOffScreen_IsScrolledIntoView()
        {
            ScrollTo(100);
            Select(_rows[104]);
            ScrollTo(700);

            Replace(_rows.Where((r, i) => i % 2 == 0));

            int index = _listView.Items.IndexOf(_rows[104]);
            int top = (int)ScrollViewer.VerticalOffset;
            Assert.That(_listView.SelectedItem, Is.SameAs(_rows[104]));
            Assert.That(index, Is.InRange(top, top + (int)ScrollViewer.ViewportHeight - 1), "the selected row is on screen");
        }

        [Test]
        public void SelectedRowFilteredOutThenBack_IsScrolledIntoViewAgain()
        {
            ScrollTo(500);
            Select(_rows[505]);
            Replace(_rows.Where((r, i) => i % 2 == 0));
            ScrollTo(10);

            Replace(_rows);

            int top = (int)ScrollViewer.VerticalOffset;
            Assert.That(_listView.SelectedItem, Is.SameAs(_rows[505]));
            Assert.That(505, Is.InRange(top, top + (int)ScrollViewer.ViewportHeight - 1));
        }

        [Test]
        public void FollowMode_ScrollsToTheLastRow()
        {
            ScrollTo(100);
            _following = true;

            Replace(_rows.Where((r, i) => i % 2 == 0));

            int lastVisible = (int)(ScrollViewer.VerticalOffset + ScrollViewer.ViewportHeight);
            Assert.That(lastVisible, Is.GreaterThanOrEqualTo(_listView.Items.Count - 1));
        }

        private ScrollViewer ScrollViewer => FindScrollViewer(_listView);

        private LogMessage TopItem()
        {
            return (LogMessage)_listView.Items[(int)ScrollViewer.VerticalOffset];
        }

        private void ScrollTo(int index)
        {
            ScrollViewer.ScrollToVerticalOffset(index);
            WaitUntil(() => (int)ScrollViewer.VerticalOffset == index);
        }

        private void Select(params LogMessage[] rows)
        {
            _listView.SelectedItem = rows[0];
            for (int i = 1; i < rows.Length; i++)
                _listView.SelectedItems.Add(rows[i]);
            Pump();
        }

        private void Replace(IEnumerable<LogMessage> rows)
        {
            var list = rows.ToList();
            _vm.Logs = list;
            WaitUntil(() => ReferenceEquals(_listView.ItemsSource, list) && !_keeper.IsRestoring);
        }

        private static void WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                Pump();
                if (condition())
                    return;
                if (DateTime.UtcNow > deadline)
                    Assert.Fail("Timed out waiting for the ListView");
                Thread.Sleep(10);
            }
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer viewer && viewer.TemplatedParent is ListView)
                    return viewer;
                var nested = FindScrollViewer(child);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private sealed class ListVm : INotifyPropertyChanged
        {
            private List<LogMessage> _logs;
            private LogMessage _selectedLog;

            public List<LogMessage> Logs
            {
                get => _logs;
                set { _logs = value; OnPropertyChanged(); }
            }

            public LogMessage SelectedLog
            {
                get => _selectedLog;
                set { _selectedLog = value; OnPropertyChanged(); }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
