using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LogViewer.Helpers
{
    /// <summary>
    /// Resets a TextBox vertical scroll to the top when the text changes.
    /// Otherwise ScrollViewer keeps VerticalOffset when switching long log messages.
    /// </summary>
    public static class TextBoxResetScroll
    {
        public static readonly DependencyProperty ResetOnTextChangeProperty =
            DependencyProperty.RegisterAttached(
                "ResetOnTextChange",
                typeof(bool),
                typeof(TextBoxResetScroll),
                new PropertyMetadata(false, OnResetOnTextChangeChanged));

        public static void SetResetOnTextChange(DependencyObject element, bool value)
        {
            element.SetValue(ResetOnTextChangeProperty, value);
        }

        public static bool GetResetOnTextChange(DependencyObject element)
        {
            return (bool)element.GetValue(ResetOnTextChangeProperty);
        }

        private static void OnResetOnTextChangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBox textBox))
                return;

            if ((bool)e.NewValue)
                textBox.TextChanged += OnTextChanged;
            else
                textBox.TextChanged -= OnTextChanged;
        }

        private static void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            Reset(textBox);
            // The new text is not measured yet: without a deferred reset the offset stays from the previous entry.
            textBox.Dispatcher.BeginInvoke(new System.Action(() => Reset(textBox)), DispatcherPriority.Loaded);
        }

        private static void Reset(TextBox textBox)
        {
            textBox.CaretIndex = 0;
            textBox.ScrollToHome();
            FindScrollViewer(textBox)?.ScrollToVerticalOffset(0);
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer viewer)
                return viewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (child != null)
                    return child;
            }

            return null;
        }
    }
}
