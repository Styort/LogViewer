using System;
using System.Collections.Generic;
using System.Windows;
using LogViewer.Enums;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for LogImportTemplate.xaml
    /// </summary>
    public partial class LogImportTemplate : Window
    {
        public Dictionary<eImportTemplateParameters, int> TemplateParameterses { get; } = new Dictionary<eImportTemplateParameters, int>();
        public LogImportTemplate()
        {
            InitializeComponent();
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            var logImportTemplateViewModel = this.DataContext as LogImportTemplateViewModel;
            if (logImportTemplateViewModel != null)
            {
                if (logImportTemplateViewModel.IsPopularTemplateSelected)
                {
                    for (int i = 0; i < logImportTemplateViewModel.SelectedPopularTemplate.Count; i++)
                    {
                        TemplateParameterses.Add(logImportTemplateViewModel.SelectedPopularTemplate[i], i);
                    }
                }
                else
                {
                    for (int i = 0; i < logImportTemplateViewModel.TemplateLogItems.Count; i++)
                    {
                        try
                        {
                            TemplateParameterses.Add(logImportTemplateViewModel.TemplateLogItems[i].SelectedTemplateParameter, i);
                        }
                        catch (Exception exception)
                        {
                            TemplateParameterses.Clear();
                            MessageBox.Show("The message template should not have the same parameters!");
                            return;
                        }
                    }
                }
            }

            this.DialogResult = true;
        }
    }
}
