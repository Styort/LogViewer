using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Linq;
using System.Reflection;
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

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for NewUpdateAvailableDialog.xaml
    /// </summary>
    public partial class NewUpdateAvailableDialog : Window
    {
        public NewUpdateAvailableDialog(UpdateCheckInfo updateInfo)
        {
            InitializeComponent();
            SizeTextBlock.Text = $"{Math.Round((double)updateInfo.UpdateSizeBytes / 1024 / 1024, 2)} MB";
            VersionTextBlock.Text = updateInfo.AvailableVersion.ToString();
        }

        public NewUpdateAvailableDialog()
        {
            InitializeComponent();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
