using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Domain;
using LogViewer.Enums;
using LogViewer.MVVM.Models;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Converts Core LogEntry to UI LogMessage and back (minimal) for filtering. Resolves Receiver (color, name) from list by port.
    /// </summary>
    public static class LogEntryConverter
    {
        public static LogMessage ToLogMessage(LogEntry entry, IList<Receiver> receivers)
        {
            if (entry == null) return null;
            var receiver = receivers?.FirstOrDefault(x => x.Port == entry.ReceiverPort) ?? new Receiver();
            var msg = new LogMessage
            {
                Time = entry.Time,
                Level = (eLogLevel)(int)entry.Level,
                Logger = entry.Logger,
                Thread = entry.Thread,
                Message = entry.Message,
                ExecutableName = entry.ExecutableName,
                Address = entry.Address,
                ProcessID = entry.ProcessID,
                Receiver = (Receiver)receiver.Clone()
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
                Message = message.Message,
                ExecutableName = message.ExecutableName,
                Address = message.Address,
                ReceiverPort = message.Receiver?.Port ?? 0
            };
        }
    }
}
