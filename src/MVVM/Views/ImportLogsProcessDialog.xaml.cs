using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for ImportFilesProcessDialog.xaml
    /// </summary>
    public partial class ImportLogsProcessDialog : Window
    {
        public event EventHandler<bool> ImportProcessDialogResult;


        public ImportLogsProcessDialog(List<ImportLogFile> importLogFiles)
        {
            InitializeComponent();
            this.DataContext = new ImportLogsProcessViewModel();
            ((ImportLogsProcessViewModel)DataContext).ImportFiles = new List<ImportLogFile>(importLogFiles);
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            OnImportProcessDialogResult(true);
            this.Close();
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            OnImportProcessDialogResult(false);
            this.Close();
        }

        protected virtual void OnImportProcessDialogResult(bool e)
        {
            ImportProcessDialogResult?.Invoke(this, e);
        }
    }
}
