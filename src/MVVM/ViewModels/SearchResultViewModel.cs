using LogViewer.Core.Services;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.Commands;
using System.Collections.ObjectModel;
using System.Windows;

namespace LogViewer.MVVM.ViewModels
{
    public class SearchResultViewModel : BaseViewModel
    {
        private LogMessage selectedLog;

        public ObservableCollection<LogMessage> SearchResult { get; set; } = new ObservableCollection<LogMessage>();

        /// <summary>
        /// Selected log.
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
        /// Text to highlight.
        /// </summary>
        public string HighlightSearchText { get; set; }

        /// <summary>
        /// Match case.
        /// </summary>
        public bool IsMatchCase { get; set; }

        public bool UseRegularExpressions { get; set; }

        public bool IsMatchWholeWord { get; set; }

        public string MessageFontFamily { get; } = Settings.Instance.MessageFontFamily;

        public double MessageFontSize { get; } = Settings.Instance.MessageFontSize;

        private RelayCommand copyMessageCommand;

        public RelayCommand CopyMessageCommand => copyMessageCommand ?? (copyMessageCommand = new RelayCommand(CopyMessage));

        /// <summary>
        /// Copies message and throwable (same as the main window) so the stack is not lost after being split from Message.
        /// </summary>
        private void CopyMessage()
        {
            if (SelectedLog == null) return;
            Clipboard.SetDataObject(LogExportText.JoinMessageAndThrowable(SelectedLog.Message, SelectedLog.Throwable));
        }

    }
}
