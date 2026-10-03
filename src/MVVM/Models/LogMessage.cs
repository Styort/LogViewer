using System;
using System.Collections.Generic;
using System.Windows.Media;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Enums;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// UI row over a Core <see cref="LogEntry"/>: the log data is read from <see cref="Entry"/>, only display
    /// state (receiver snapshot, marks, bookmark) lives here.
    /// </summary>
    /// <remarks>
    /// The row does not copy the entry: a buffer holds up to millions of rows, and a second copy of every field
    /// cost memory and let the UI view drift from Core. Entries are never mutated after they are received, so
    /// the data properties are read-only and raise no change notifications.
    /// </remarks>
    public class LogMessage : BaseViewModel
    {
        public LogMessage(LogEntry entry, Receiver receiver = null)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Receiver = receiver ?? new Receiver();
        }

        /// <summary>
        /// Core entry this row was projected from. Core navigation and grouping read it directly instead of
        /// converting rows back.
        /// </summary>
        public LogEntry Entry { get; }

        /// <summary>
        /// Time the log was received.
        /// </summary>
        public DateTime Time => Entry.Time;

        /// <summary>
        /// <see cref="LogEntry.Sequence"/> of the Core entry. Maps a filtered Core snapshot back to the same row instances.
        /// </summary>
        public long Sequence => Entry.Sequence;

        /// <summary>
        /// Log level.
        /// </summary>
        public eLogLevel Level => (eLogLevel)(int)Entry.Level;

        /// <summary>
        /// Logger / class the message came from.
        /// </summary>
        public string Logger => Entry.Logger;

        /// <summary>
        /// Thread id.
        /// </summary>
        public int Thread => Entry.Thread;

        /// <summary>
        /// Message text.
        /// </summary>
        public string Message => Entry.Message;

        public string ExecutableName => Entry.ExecutableName;

        /// <summary>
        /// IP address of the device that sent the message.
        /// </summary>
        public string Address => Entry.Address;

        /// <summary>
        /// Cached by the entry: the tree, marks and export read it for every row.
        /// </summary>
        public string FullPath => Entry.FullPath;

        public int? ProcessID => Entry.ProcessID;

        /// <summary>
        /// Exception text from XML (not part of Message). Empty string if the element was absent.
        /// </summary>
        public string Throwable => Entry.Throwable;

        /// <summary>
        /// MDC/event properties. Never null; not shown in the details pane.
        /// </summary>
        public Dictionary<string, string> Properties => Entry.Properties ?? EmptyProperties;

        /// <summary>
        /// Details pane and copy text: Message, then Throwable on a new line — same visual as before.
        /// Throwable is not stored inside Message (search/grouping).
        /// </summary>
        public string MessageWithThrowable => LogExportText.JoinMessageAndThrowable(Message, Throwable);

        /// <summary>
        /// Display snapshot of the receiver (color, name). Shared by every row of the same receiver,
        /// see <see cref="LogViewer.Adapters.LogEntryProjector"/>; do not mutate it per row.
        /// </summary>
        public Receiver Receiver { get; }

        // Shared and never handed out for writing: Properties is read-only for rows.
        private static readonly Dictionary<string, string> EmptyProperties = new Dictionary<string, string>();

        // One frozen brush for every row: a log buffer holds up to millions of rows, and two
        // brushes per row used to cost more memory than the log text itself.
        private static readonly SolidColorBrush TransparentBrush = CreateTransparentBrush();

        private SolidColorBrush toggleMark = TransparentBrush;
        private SolidColorBrush rowBackground = TransparentBrush;
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

        private static SolidColorBrush CreateTransparentBrush()
        {
            var brush = new SolidColorBrush(Colors.Transparent);
            brush.Freeze();
            return brush;
        }
    }
}
