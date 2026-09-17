using System;
using LogViewer.Enums;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    public class LogBookmark : BaseViewModel
    {
        private LogMessage log;
        private string comment = string.Empty;

        public Guid Id { get; } = Guid.NewGuid();

        public DateTime CreatedAt { get; } = DateTime.Now;

        public LogMessage Log
        {
            get => log;
            set
            {
                log = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Time));
                OnPropertyChanged(nameof(Level));
                OnPropertyChanged(nameof(Logger));
                OnPropertyChanged(nameof(MessagePreview));
            }
        }

        public string Comment
        {
            get => comment;
            set
            {
                comment = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public DateTime Time => log?.Time ?? default;

        public eLogLevel? Level => log?.Level;

        public string Logger => log?.Logger;

        public string MessagePreview
        {
            get
            {
                var message = log?.Message;
                if (string.IsNullOrEmpty(message))
                    return string.Empty;

                var newLine = message.IndexOf('\n');
                if (newLine >= 0)
                    message = message.Substring(0, newLine);

                return message.Length > 120 ? message.Substring(0, 120) + "..." : message;
            }
        }
    }
}
