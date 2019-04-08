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

        /// <summary>
        /// Подсвечиваемый текст
        /// </summary>
        public string HighlightSearchText { get; set; }

        /// <summary>
        /// Учитывать регистр
        /// </summary>
        public bool IsMatchCase { get; set; }
    }
}
