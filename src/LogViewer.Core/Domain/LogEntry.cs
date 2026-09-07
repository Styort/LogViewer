using System;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Plain DTO for a log entry. No UI dependencies.
    /// </summary>
    public class LogEntry
    {
        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }
        public string Logger { get; set; }
        public int Thread { get; set; }
        public string Message { get; set; }
        public string ExecutableName { get; set; }
        public string Address { get; set; }
        public int? ProcessID { get; set; }

        /// <summary>
        /// Receiver identifier (e.g. port) so UI can resolve color/name from Settings.
        /// </summary>
        public int ReceiverPort { get; set; }

        public string FullPath
        {
            get
            {
                if (string.IsNullOrEmpty(ExecutableName))
                    return Address + "." + Logger;
                return Address + "." + ExecutableName + "." + Logger;
            }
        }
    }
}
