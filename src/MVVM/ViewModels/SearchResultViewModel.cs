using LogViewer.MVVM.Models;
using System.Collections.ObjectModel;

namespace LogViewer.MVVM.ViewModels
{
    public class SearchResultViewModel : BaseViewModel
    {
        private LogMessage selectedLog;

        public ObservableCollection<LogMessage> SearchResult { get; set; } = new ObservableCollection<LogMessage>();

        /// <summary>
        /// Выбранный лог
        /// </summary>
        public LogMessage SelectedLog
        {
            get => selectedLog;
            set
            {
                selectedLog = value;
                OnPropertyChanged();
            }
        }
    }
}
