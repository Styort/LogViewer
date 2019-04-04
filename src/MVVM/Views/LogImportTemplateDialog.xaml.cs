using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;
using NLog;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for LogImportTemplate.xaml
    /// </summary>
    public partial class LogImportTemplateDialog : Window
    {
        public LogTemplate LogTemplate { get; private set; }
        public bool NeedUpdateFile { get; private set; } = false;
 
        public LogImportTemplateDialog(string path)
        {
            InitializeComponent();
            DataContext = new LogImportTemplateViewModel(path);
        }

        private void LogImportTemplateDialog_OnClosing(object sender, CancelEventArgs e)
        {
            var logImportTemplateViewModel = this.DataContext as LogImportTemplateViewModel;
            if (logImportTemplateViewModel != null)
            {
                LogTemplate = logImportTemplateViewModel.LogTemplate;
                NeedUpdateFile = logImportTemplateViewModel.NeedUpdateFile;
            }
        }
    }
}
