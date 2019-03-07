using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LogViewer.Enums;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;

namespace LogViewer.MVVM.ViewModels
{
    public class LogImportTemplateViewModel : BaseViewModel
    {
        public Dictionary<string, List<eImportTemplateParameters>> PopularTemplates { get; set; } = new Dictionary<string, List<eImportTemplateParameters>>();
        public List<eImportTemplateParameters> SelectedPopularTemplate { get; set; }

        public ObservableCollection<LogTemplateItem> TemplateLogItems { get; set; } = new ObservableCollection<LogTemplateItem>();
        public bool IsPopularTemplateSelected { get; set; } = true;
        public bool IsUserTemplateSelected { get; set; }


        public LogImportTemplateViewModel()
        {
            PopularTemplates.Add("DateTime;LogLevel;EventID;ProcessID;ThreadNumber;Callsite;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.EventID,
                    eImportTemplateParameters.ProcessID,
                    eImportTemplateParameters.ThreadNumber,
                    eImportTemplateParameters.Callsite,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;ThreadNumber;Callsite;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.ThreadNumber,
                    eImportTemplateParameters.Callsite,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;ThreadNumber;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.ThreadNumber,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;Callsite;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.Callsite,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            SelectedPopularTemplate = PopularTemplates.First().Value;
        }

        private RelayCommand addTemplateItemCommand;
        private RelayCommand removeTemplateItemCommand;

        public RelayCommand AddTemplateItemCommand => addTemplateItemCommand ?? (addTemplateItemCommand = new RelayCommand(AddTemplateItem));
        public RelayCommand RemoveTemplateItemCommand => removeTemplateItemCommand ?? (removeTemplateItemCommand = new RelayCommand(RemoveTemplateItem));

        private void AddTemplateItem(object obj)
        {
            TemplateLogItems.Add(new LogTemplateItem());
        }

        private void RemoveTemplateItem(object obj)
        {
            LogTemplateItem item = obj as LogTemplateItem;
            if (item != null)
                TemplateLogItems.Remove(item);
        }

    }
}
