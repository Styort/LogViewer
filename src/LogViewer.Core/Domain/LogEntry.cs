using System;
using System.Collections.Generic;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Plain DTO for a log entry. No UI dependencies.
    /// </summary>
    public class LogEntry
    {
        private string _logger;
        private string _executableName;
        private string _address;
        private string _fullPath;

        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }

        public string Logger
        {
            get { return _logger; }
            set { _logger = value; _fullPath = null; }
        }

        public int Thread { get; set; }
        public string Message { get; set; }

        public string ExecutableName
        {
            get { return _executableName; }
            set { _executableName = value; _fullPath = null; }
        }

        public string Address
        {
            get { return _address; }
            set { _address = value; _fullPath = null; }
        }

        public int? ProcessID { get; set; }

        /// <summary>
        /// Raw throwable/stack text when present. Empty until the event-properties parser (task 04) fills it.
        /// Not part of <see cref="Message"/>. Search includes this field.
        /// </summary>
        public string Throwable { get; set; }

        /// <summary>
        /// MDC / event data. Empty until task 04. Search uses values only (not keys, not <see cref="FullPath"/>).
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Receiver identifier (e.g. port) so UI can resolve color/name from Settings.
        /// </summary>
        public int ReceiverPort { get; set; }

        public string FullPath
        {
            get
            {
                if (_fullPath == null)
                {
                    if (string.IsNullOrEmpty(_executableName))
                        _fullPath = _address + "." + _logger;
                    else
                        _fullPath = _address + "." + _executableName + "." + _logger;
                }
                return _fullPath;
            }
        }
    }
}
