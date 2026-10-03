using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class LiveXmlLogEntryFactoryTests
    {
        private readonly XmlLogParser _parser = new XmlLogParser();

        [Test]
        public void Create_BrokenFrame_ReturnsErrorAndDoesNotThrow()
        {
            LogEntry entry = null;

            Assert.DoesNotThrow(() =>
            {
                entry = LiveXmlLogEntryFactory.Create(
                    _parser,
                    "<foo/>",
                    "10.0.0.1",
                    7071,
                    ReceiverTransport.Tcp,
                    "TCP Logger");
            });

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Logger, Is.EqualTo("TCP Logger"));
            Assert.That(entry.Address, Is.EqualTo("10.0.0.1"));
            Assert.That(entry.ReceiverPort, Is.EqualTo(7071));
            Assert.That(entry.ReceiverTransport, Is.EqualTo(ReceiverTransport.Tcp));
            Assert.That(entry.Message, Does.Contain("<foo/>"));
        }

        [Test]
        public void Create_EmptyFrame_ReturnsErrorAndDoesNotThrow()
        {
            LogEntry entry = LiveXmlLogEntryFactory.Create(_parser, "   ", "127.0.0.1", 1, ReceiverTransport.Tcp, "TCP Logger");
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        }

        [Test]
        public void Create_EventWithoutLogger_UsesFallbackLogger()
        {
            // A single packet like this used to crash the UI thread in the logger tree.
            const string xml = "<log4j:event level=\"INFO\" timestamp=\"1\"><log4j:message>x</log4j:message></log4j:event>";

            LogEntry entry = LiveXmlLogEntryFactory.Create(_parser, xml, "10.0.0.1", 7071, ReceiverTransport.Udp, "UDP Logger");

            Assert.That(entry.Logger, Is.EqualTo("UDP Logger"));
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Info));
            Assert.That(entry.Message, Is.EqualTo("x"));
            Assert.That(entry.FullPath, Is.EqualTo("10.0.0.1.UDP Logger"));
        }

        [Test]
        public void Create_EventWithoutAnyAttributesOrMessage_IsNormalized()
        {
            const string xml = "<log4j:event></log4j:event>";

            LogEntry entry = LiveXmlLogEntryFactory.Create(_parser, xml, "10.0.0.1", 7071, ReceiverTransport.Tcp, null);

            Assert.That(entry.Logger, Is.EqualTo(LiveXmlLogEntryFactory.DefaultFallbackLogger));
            Assert.That(entry.Message, Is.Not.Null);
            Assert.That(entry.Time, Is.Not.EqualTo(default(System.DateTime)));
        }

        [Test]
        public void Create_EventWithWhitespaceLogger_UsesFallbackLogger()
        {
            const string xml = "<log4j:event logger=\" \" level=\"INFO\" timestamp=\"1\"><log4j:message>x</log4j:message></log4j:event>";

            LogEntry entry = LiveXmlLogEntryFactory.Create(_parser, xml, "10.0.0.1", 7071, ReceiverTransport.Tcp, "TCP Logger");

            Assert.That(entry.Logger, Is.EqualTo("TCP Logger"));
        }
    }

    [TestFixture]
    public class ReceiverTransportHelperTests
    {
        [Test]
        public void ParseOrUdp_NullOrEmpty_IsUdp()
        {
            Assert.That(ReceiverTransportHelper.ParseOrUdp(null), Is.EqualTo(ReceiverTransport.Udp));
            Assert.That(ReceiverTransportHelper.ParseOrUdp(""), Is.EqualTo(ReceiverTransport.Udp));
            Assert.That(ReceiverTransportHelper.ParseOrUdp("  "), Is.EqualTo(ReceiverTransport.Udp));
        }

        [Test]
        public void ParseOrUdp_Unknown_IsUdp()
        {
            Assert.That(ReceiverTransportHelper.ParseOrUdp("Sctp"), Is.EqualTo(ReceiverTransport.Udp));
            Assert.That(ReceiverTransportHelper.ParseOrUdp("99"), Is.EqualTo(ReceiverTransport.Udp));
        }

        [Test]
        public void ParseOrUdp_Tcp_IsCaseInsensitive()
        {
            Assert.That(ReceiverTransportHelper.ParseOrUdp("tcp"), Is.EqualTo(ReceiverTransport.Tcp));
            Assert.That(ReceiverTransportHelper.ParseOrUdp("TCP"), Is.EqualTo(ReceiverTransport.Tcp));
        }
    }
}
