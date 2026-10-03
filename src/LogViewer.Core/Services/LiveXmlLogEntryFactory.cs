using System;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Parses a complete log4j XML frame for UDP/TCP live sources.
    /// Parse failures become a synthetic Error row; the receive loop must not stop.
    /// </summary>
    /// <remarks>
    /// This is the trust boundary for network input. Every entry leaving it has a non-empty
    /// <see cref="LogEntry.Logger"/>, a non-null <see cref="LogEntry.Message"/>, and a real timestamp:
    /// the logger tree splits <c>Logger</c> on the UI thread, and a single event without the
    /// <c>logger</c> attribute used to crash the whole application there.
    /// </remarks>
    public static class LiveXmlLogEntryFactory
    {
        public const string DefaultFallbackLogger = "Network Logger";

        public static LogEntry Create(
            ILogParser parser,
            string xml,
            string address,
            int receiverPort,
            ReceiverTransport transport,
            string fallbackLogger)
        {
            if (string.IsNullOrEmpty(fallbackLogger))
                fallbackLogger = DefaultFallbackLogger;

            LogEntry entry;
            try
            {
                entry = parser.Parse(xml);
            }
            catch (Exception ex)
            {
                entry = new LogEntry
                {
                    Logger = fallbackLogger,
                    Address = address,
                    Thread = -1,
                    Message = $"An error occurred while parsing log: {xml}. {Environment.NewLine} Exception: {ex}",
                    Time = DateTime.Now,
                    Level = LogLevel.Error,
                    ExecutableName = "LogViewer"
                };
            }

            if (string.IsNullOrWhiteSpace(entry.Logger))
                entry.Logger = fallbackLogger;
            if (entry.Message == null)
                entry.Message = string.Empty;
            if (entry.Time == default(DateTime))
                entry.Time = DateTime.Now;

            entry.Address = address;
            entry.ReceiverPort = receiverPort;
            entry.ReceiverTransport = transport;
            return entry;
        }
    }
}
