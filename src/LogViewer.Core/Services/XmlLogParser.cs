using System;
using System.IO;
using System.Xml;
using System.Text;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Parses a Chainsaw / NLogViewer / log4j XML event fragment into <see cref="LogEntry"/>.
    /// </summary>
    /// <remarks>
    /// <c>log4j:throwable</c> is stored on <see cref="LogEntry.Throwable"/>, not concatenated into
    /// <see cref="LogEntry.Message"/>. Keeping them apart lets search, grouping, and the stacktrace UI
    /// treat the exception independently. Clipboard and .txt export join them again via
    /// <see cref="LogExportText"/>.
    /// <para>
    /// The parser is stateless and thread-safe: one instance is shared by every UDP/TCP receive thread.
    /// <see cref="NameTable"/> and <see cref="XmlNamespaceManager"/> are not thread-safe (the reader
    /// pushes and pops a namespace scope for every <c>xmlns:log4j</c> declaration), so the parser
    /// context is created per call instead of being cached in a field.
    /// </para>
    /// </remarks>
    public class XmlLogParser : ILogParser
    {
        /// <summary>Used when the event has no <c>level</c> attribute or an unknown one.</summary>
        public const LogLevel DefaultLevel = LogLevel.Info;

        // XmlReaderSettings is immutable once handed to XmlReader.Create, so one instance can be shared.
        private static readonly XmlReaderSettings ReaderSettings = new XmlReaderSettings
        {
            ConformanceLevel = ConformanceLevel.Fragment,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = true
        };

        public LogEntry Parse(string xmlFragment)
        {
            var log = new LogEntry { Level = DefaultLevel };

            using (var reader = XmlReader.Create(new StringReader(xmlFragment ?? string.Empty), ReaderSettings, CreateContext()))
            {
                if (reader.MoveToContent() != XmlNodeType.Element || reader.Name != "log4j:event")
                    throw new Exception("The Log Event is not a valid log4j Xml block.");

                log.Logger = reader.GetAttribute("logger");
                log.Level = ParseLevel(reader.GetAttribute("level"));

                var threadAttr = reader.GetAttribute("thread");
                if (!string.IsNullOrEmpty(threadAttr) && int.TryParse(threadAttr, out int threadVal))
                    log.Thread = threadVal;

                long timeStamp;
                if (long.TryParse(reader.GetAttribute("timestamp"), out timeStamp))
                    log.Time = UnixTimeStampToDateTime(timeStamp).ToLocalTime();

                int eventDepth = reader.Depth;
                reader.Read();
                while (reader.Depth > eventDepth)
                {
                    if (reader.MoveToContent() == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "log4j:message":
                                log.Message = reader.ReadString();
                                break;
                            case "log4j:throwable":
                                log.Throwable = reader.ReadString();
                                break;
                            case "log4j:properties":
                                ReadProperties(reader, log);
                                break;
                        }
                    }
                    reader.Read();
                }
            }
            return log;
        }

        /// <summary>
        /// Reads each <c>log4j:data</c> name/value into <see cref="LogEntry.Properties"/>.
        /// Empty names are skipped. <c>log4japp</c> still fills <see cref="LogEntry.ExecutableName"/>
        /// (logger tree) and is also kept in the dictionary.
        /// </summary>
        private static void ReadProperties(XmlReader reader, LogEntry log)
        {
            if (log.Properties == null)
                log.Properties = new System.Collections.Generic.Dictionary<string, string>();

            reader.Read();
            while (reader.MoveToContent() == XmlNodeType.Element && reader.Name == "log4j:data")
            {
                string name = reader.GetAttribute("name");
                string value = reader.GetAttribute("value") ?? string.Empty;
                if (!string.IsNullOrEmpty(name))
                {
                    log.Properties[name] = value;
                    if (name == "log4japp" && !string.IsNullOrEmpty(value))
                    {
                        int idx = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                            log.ExecutableName = value.Substring(0, idx);
                    }
                }
                reader.Read();
            }
        }

        /// <summary>
        /// Maps a log4j/NLog level name to <see cref="LogLevel"/>. Unknown or missing values fall back to
        /// <see cref="DefaultLevel"/>: a sender with a custom level must not turn every event into a parse error.
        /// </summary>
        public static LogLevel ParseLevel(string levelAttr)
        {
            if (string.IsNullOrWhiteSpace(levelAttr))
                return DefaultLevel;

            switch (levelAttr.Trim().ToUpperInvariant())
            {
                case "TRACE":
                case "ALL":
                case "FINEST":
                case "FINER":
                    return LogLevel.Trace;
                case "DEBUG":
                case "FINE":
                    return LogLevel.Debug;
                case "INFO":
                case "INFORMATION":
                case "NOTICE":
                    return LogLevel.Info;
                case "WARN":
                case "WARNING":
                    return LogLevel.Warn;
                case "ERROR":
                case "SEVERE":
                    return LogLevel.Error;
                case "FATAL":
                case "CRITICAL":
                case "EMERGENCY":
                    return LogLevel.Fatal;
                default:
                    return DefaultLevel;
            }
        }

        private static XmlParserContext CreateContext()
        {
            var nt = new NameTable();
            var nsmanager = new XmlNamespaceManager(nt);
            nsmanager.AddNamespace("log4j", "http://jakarta.apache.org/log4j/");
            nsmanager.AddNamespace("nlog", "http://nlog-project.org");
            return new XmlParserContext(nt, nsmanager, null, XmlSpace.None, Encoding.UTF8);
        }

        private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixTimeStamp);
        }
    }
}
