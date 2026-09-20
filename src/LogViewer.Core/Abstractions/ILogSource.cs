using System;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// Produces log entries. Implementations run on background threads; no UI.
    /// </summary>
    public interface ILogSource
    {
        void Start();
        void Stop();
        /// <summary>
        /// Raised when a new log entry is available. Raised on a background thread
        /// (UDP receive, TCP accept/read). UI batching marshals onto the dispatcher.
        /// </summary>
        event EventHandler<LogEntryReceivedEventArgs> LogReceived;
    }

    public class LogEntryReceivedEventArgs : EventArgs
    {
        public LogEntry Entry { get; set; }
    }
}
