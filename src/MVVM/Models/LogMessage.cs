using System;
using System.Windows.Media;
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

        public int? EventID { get; set; }
        public int? ProcessID { get; set; }

        public Receiver Receiver { get; set; } = new Receiver();

        private SolidColorBrush toggleMark = new SolidColorBrush(Colors.Transparent);

        public SolidColorBrush ToggleMark
        {
            get => toggleMark;
            set
            {
                toggleMark = value;
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
                Receiver = (Receiver)this.Receiver.Clone()
            };
        }
    }
}
