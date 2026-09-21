using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using LogViewer.MVVM.Models;

namespace LogViewer.MVVM.ViewModels
{
    public class ImportLogsProcessViewModel : BaseViewModel
    {
        private List<ImportLogFile> _importFiles = new List<ImportLogFile>();
        private bool _okIsEnabled;

        public List<ImportLogFile> ImportFiles
        {
            get => _importFiles;
            set
            {
                Unsubscribe();
                _importFiles = value ?? new List<ImportLogFile>();
                Subscribe();
                RefreshOkIsEnabled();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// OK stays disabled until every file reports 100. Cancel is the way to abort a running import.
        /// </summary>
        public bool OkIsEnabled
        {
            get => _okIsEnabled;
            private set
            {
                if (_okIsEnabled == value)
                    return;
                _okIsEnabled = value;
                OnPropertyChanged();
            }
        }

        private void Subscribe()
        {
            foreach (var file in _importFiles)
                file.PropertyChanged += OnImportFilePropertyChanged;
        }

        private void Unsubscribe()
        {
            foreach (var file in _importFiles)
                file.PropertyChanged -= OnImportFilePropertyChanged;
        }

        private void OnImportFilePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ImportLogFile.Process))
                RefreshOkIsEnabled();
        }

        private void RefreshOkIsEnabled()
        {
            OkIsEnabled = _importFiles.Count > 0 && _importFiles.All(file => file.Process >= 100);
        }
    }
}
