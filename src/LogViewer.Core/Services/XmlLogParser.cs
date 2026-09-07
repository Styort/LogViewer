using System;
using System.Xml;
using System.Text;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.Services
{
    public class XmlLogParser : ILogParser
    {
        private readonly XmlParserContext _xmlContext;

        public XmlLogParser()
        {
            _xmlContext = CreateContext();
        }

        public LogEntry Parse(string xmlFragment)
        {
            var log = new LogEntry();

            using (var reader = new XmlTextReader(xmlFragment, XmlNodeType.Element, _xmlContext))
            {
                reader.Read();
                if (reader.MoveToContent() != XmlNodeType.Element || reader.Name != "log4j:event")
                    throw new Exception("The Log Event is not a valid log4j Xml block.");

                log.Logger = reader.GetAttribute("logger");
                var levelAttr = reader.GetAttribute("level");
                if (!string.IsNullOrEmpty(levelAttr))
                    log.Level = (LogLevel)Enum.Parse(typeof(LogLevel), StringUtils.FirstCharToUpper(levelAttr.ToLowerInvariant()));

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
                                log.Message += Environment.NewLine + reader.ReadString();
                                break;
                            case "log4j:properties":
                                reader.Read();
                                while (reader.MoveToContent() == XmlNodeType.Element && reader.Name == "log4j:data")
                                {
                                    string name = reader.GetAttribute("name");
                                    string value = reader.GetAttribute("value");
                                    if (!string.IsNullOrEmpty(name) && name == "log4japp" && !string.IsNullOrEmpty(value) && value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        int idx = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                                        log.ExecutableName = value.Substring(0, idx);
                                    }
                                    reader.Read();
                                }
                                break;
                        }
                    }
                    reader.Read();
                }
            }
            return log;
        }

        private static XmlParserContext CreateContext()
        {
            var nt = new NameTable();
            var nsmanager = new XmlNamespaceManager(nt);
            nsmanager.AddNamespace("log4j", "http://jakarta.apache.org/log4j/");
            nsmanager.AddNamespace("nlog", "http://nlog-project.org");
            return new XmlParserContext(nt, nsmanager, "elem", XmlSpace.None, Encoding.UTF8);
        }

        private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixTimeStamp);
        }
    }
}
