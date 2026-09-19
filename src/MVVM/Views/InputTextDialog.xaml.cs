using System.Windows;

namespace LogViewer.MVVM.Views
{
    public partial class InputTextDialog : Window
    {
        public string Text => ValueTextBox.Text ?? string.Empty;

        public InputTextDialog(string title, string prompt, string initial)
        {
            InitializeComponent();
            Title = title ?? string.Empty;
            PromptText.Text = prompt ?? string.Empty;
            ValueTextBox.Text = initial ?? string.Empty;
            Loaded += (sender, args) =>
            {
                ValueTextBox.Focus();
                ValueTextBox.SelectAll();
            };
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
