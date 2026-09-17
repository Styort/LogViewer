using System.Windows;

namespace LogViewer.MVVM.Views
{
    public partial class BookmarkCommentDialog : Window
    {
        public string Comment => CommentTextBox.Text ?? string.Empty;

        public BookmarkCommentDialog(string comment)
        {
            InitializeComponent();
            CommentTextBox.Text = comment ?? string.Empty;
            Loaded += (sender, args) =>
            {
                CommentTextBox.Focus();
                CommentTextBox.SelectAll();
            };
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
