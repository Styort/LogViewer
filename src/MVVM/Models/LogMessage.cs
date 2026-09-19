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
        /// Time the log was received.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Log level.
        /// </summary>
        public eLogLevel Level { get; set; }

        /// <summary>
        /// Logger / class the message came from.
        /// </summary>
        public string Logger { get; set; }

        /// <summary>
        /// Thread id.
        /// </summary>
        public int Thread { get; set; }

        /// <summary>
        /// Message text.
        /// </summary>
        public string Message { get; set; }

        public string ExecutableName { get; set; }

        /// <summary>
        /// IP address of the device that sent the message.
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
        /// Exception text from XML (not part of Message). Empty string if the element was absent.
        /// </summary>
        public string Throwable { get; set; }

        /// <summary>
        /// MDC/event properties. Never null; not shown in the details pane.
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Details pane and copy text: Message, then Throwable on a new line — same visual as before.
        /// Throwable is not stored inside Message (search/grouping).
        /// </summary>
        public string MessageWithThrowable => LogExportText.JoinMessageAndThrowable(Message, Throwable);

        public Receiver Receiver { get; set; } = new Receiver();

        private SolidColorBrush toggleMark = new SolidColorBrush(Colors.Transparent);
        private SolidColorBrush rowBackground = new SolidColorBrush(Colors.Transparent);
        private bool hasBookmark;

        public SolidColorBrush ToggleMark
        {
            get => toggleMark;
            set
            {
                toggleMark = value;
                if (toggleMark != null && !toggleMark.IsFrozen)
                    toggleMark.Freeze();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ListView row fill. Separate from <see cref="ToggleMark"/> so tree marks survive rule recolor.
        /// Frozen for virtualization.
        /// </summary>
        public SolidColorBrush RowBackground
        {
            get => rowBackground;
            set
            {
                rowBackground = value;
                if (rowBackground != null && !rowBackground.IsFrozen)
                    rowBackground.Freeze();
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
            rowBackground.Freeze();
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
