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

        public bool UseRegularExpressions { get; set; }

        public bool IsMatchWholeWord { get; set; }

        public string MessageFontFamily { get; } = Settings.Instance.MessageFontFamily;

        public double MessageFontSize { get; } = Settings.Instance.MessageFontSize;

        private RelayCommand copyMessageCommand;

        public RelayCommand CopyMessageCommand => copyMessageCommand ?? (copyMessageCommand = new RelayCommand(CopyMessage));

        /// <summary>
        /// Копирует сообщение и throwable (как в основном окне), чтобы stack не терялся после выноса из Message.
        /// </summary>
        private void CopyMessage()
        {
            if (SelectedLog == null) return;
            Clipboard.SetDataObject(LogExportText.JoinMessageAndThrowable(SelectedLog.Message, SelectedLog.Throwable));
        }

    }
}
