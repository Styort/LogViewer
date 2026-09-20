using System;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Parses a complete log4j XML frame for UDP/TCP live sources.
    /// Parse failures become a synthetic Error row; the receive loop must not stop.
    /// </summary>
    public static class LiveXmlLogEntryFactory
    {
        public static LogEntry Create(
            ILogParser parser,
            string xml,
            string address,
            int receiverPort,
            ReceiverTransport transport,
            string fallbackLogger)
        {
            LogEntry entry;
            try
            {
                entry = parser.Parse(xml);
            }
            catch (Exception ex)
            {
                entry = new LogEntry
                {
                    Logger = fallbackLogger ?? "Network Logger",
                    Address = address,
                    Thread = -1,
                    Message = $"An error occurred while parsing log: {xml}. {Environment.NewLine} Exception: {ex}",
                    Time = DateTime.Now,
                    Level = LogLevel.Error,
                    ExecutableName = "LogViewer"
                };
            }

            entry.Address = address;
            entry.ReceiverPort = receiverPort;
            entry.ReceiverTransport = transport;
            return entry;
        }
    }
}
