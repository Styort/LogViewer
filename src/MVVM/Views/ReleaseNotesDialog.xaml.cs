using System;
using System.Windows;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for ReleaseNotesDialog.xaml
    /// </summary>
    public partial class ReleaseNotesDialog : Window
    {
        public ReleaseNotesDialog()
        {
            InitializeComponent();
            ContentRendered += OnFirstContentRendered;
        }

        private void OnFirstContentRendered(object sender, EventArgs e)
        {
            ContentRendered -= OnFirstContentRendered;
            DataContext = new ReleaseNotesViewModel();
        }
    }
}
