using System;
using System.Collections.Generic;
using System.Windows.Media;
using LogViewer.Core.Services;
using LogViewer.Enums;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    public class LogMessage : BaseViewModel, ICloneable
    {
        /// <summary>
        /// Время получения лога
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Уроверь лога
        /// </summary>
        public eLogLevel Level { get; set; }

        /// <summary>
        /// Класс, из которого пришло сообщение
        /// </summary>
        public string Logger { get; set; }

        /// <summary>
        /// Номер потока
        /// </summary>
        public int Thread { get; set; }

        /// <summary>
        /// Сообщение
        /// </summary>
        public string Message { get; set; }

        public string ExecutableName { get; set; }

        /// <summary>
        /// IP-Адрес устройства, с которого пришло сообщение
        /// </summary>
        public string Address { get; set; }

        public string FullPath
        {
            get
            {
                if (string.IsNullOrEmpty(ExecutableName))
                    return Address + "." + Logger;
                return Address + "." + ExecutableName + "." + Logger;
            }
        }

        public int? ProcessID { get; set; }

        /// <summary>
        /// Текст исключения из XML (не часть Message). Пустая строка, если элемента не было.
        /// </summary>
        public string Throwable { get; set; }

        /// <summary>
        /// MDC/свойства события. Никогда не null; в панели деталей не показываются.
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Текст панели деталей и копирования: Message, затем Throwable с новой строки — как раньше визуально.
        /// В самом Message throwable не хранится (поиск/группировка).
        /// </summary>
        public string MessageWithThrowable => LogExportText.JoinMessageAndThrowable(Message, Throwable);

        public Receiver Receiver { get; set; } = new Receiver();

        private SolidColorBrush toggleMark = new SolidColorBrush(Colors.Transparent);
        private bool hasBookmark;

        public SolidColorBrush ToggleMark
        {
            get => toggleMark;
            set
            {
                toggleMark = value;
                toggleMark.Freeze();
                OnPropertyChanged();
            }
        }

        public bool HasBookmark
        {
            get => hasBookmark;
            set
            {
                hasBookmark = value;
                OnPropertyChanged();
            }
        }

        public LogMessage()
        {
            toggleMark.Freeze();
        }

        public object Clone()
        {
            return new LogMessage
            {
                Address = this.Address,
                ExecutableName = this.ExecutableName,
                Level = this.Level,
                Logger = this.Logger,
                Message = this.Message,
                Thread = this.Thread,
                Time = this.Time,
                Throwable = this.Throwable,
                Properties = this.Properties != null
                    ? new Dictionary<string, string>(this.Properties)
                    : new Dictionary<string, string>(),
                Receiver = (Receiver)this.Receiver.Clone()
            };
        }
    }
}
