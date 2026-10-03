using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Enums;
using LogViewer.MVVM.Models;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Converts Core LogEntry to UI LogMessage and back (minimal) for filtering.
    /// </summary>
    /// <remarks>
    /// <see cref="LogEntry.Properties"/> is shared by reference in both directions, not copied: entries are
    /// never mutated after they are received, and a per-row dictionary copy doubled the memory of MDC data.
    /// </remarks>
    public static class LogEntryConverter
    {
        /// <param name="receiver">Display snapshot shared by every row of this receiver (see <see cref="LogEntryProjector"/>).</param>
        public static LogMessage ToLogMessage(LogEntry entry, Receiver receiver)
        {
            if (entry == null) return null;
            var msg = new LogMessage
            {
                Sequence = entry.Sequence,
                Time = entry.Time,
                Level = (eLogLevel)(int)entry.Level,
                Logger = entry.Logger,
                Thread = entry.Thread,
                Message = entry.Message,
                ExecutableName = entry.ExecutableName,
                Address = entry.Address,
                ProcessID = entry.ProcessID,
                Throwable = entry.Throwable,
                Properties = entry.Properties ?? new Dictionary<string, string>(),
                Receiver = receiver ?? new Receiver()
            };
            return msg;
        }

        /// <summary>
        /// Minimal LogEntry from LogMessage for re-applying Core filter (e.g. when criteria change).
        /// </summary>
        public static LogEntry ToLogEntry(LogMessage message)
        {
            if (message == null) return null;
            return new LogEntry
            {
                Time = message.Time,
                Level = (LogLevel)(int)message.Level,
                Logger = message.Logger,
                Thread = message.Thread,
                Message = message.Message,
                ExecutableName = message.ExecutableName,
                Address = message.Address,
                ReceiverPort = message.Receiver?.Port ?? 0,
                ReceiverTransport = message.Receiver?.Transport ?? ReceiverTransport.Udp,
                Throwable = message.Throwable,
                Properties = message.Properties ?? new Dictionary<string, string>()
            };
        }
    }
}
