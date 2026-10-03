using LogViewer.Core.Domain;
using LogViewer.MVVM.Models;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Wraps a Core LogEntry in a UI LogMessage row.
    /// </summary>
    /// <remarks>
    /// The row references the entry instead of copying it: entries are never mutated after they are received,
    /// and Core services (navigation, grouping) read <see cref="LogMessage.Entry"/> without a conversion back.
    /// </remarks>
    public static class LogEntryConverter
    {
        /// <param name="receiver">Display snapshot shared by every row of this receiver (see <see cref="LogEntryProjector"/>).</param>
        public static LogMessage ToLogMessage(LogEntry entry, Receiver receiver)
        {
            if (entry == null) return null;
            return new LogMessage(entry, receiver);
        }
    }
}
