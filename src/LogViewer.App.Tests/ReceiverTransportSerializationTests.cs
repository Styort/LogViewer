using System.IO;
using System.Xml.Serialization;
using LogViewer.Core.Domain;
using LogViewer.MVVM.Models;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    public class ReceiverTransportSerializationTests
    {
        [Test]
        public void Deserialize_MissingTransport_IsUdp()
        {
            const string xml =
                "<Receiver><Name>UDP Receiver</Name><Port>7071</Port><IsActive>true</IsActive>" +
                "<Encoding>UTF-8</Encoding><ColorString>#FFFFFFFF</ColorString></Receiver>";

            var receiver = Deserialize(xml);

            Assert.That(receiver.Transport, Is.EqualTo(ReceiverTransport.Udp));
        }

        [Test]
        public void Deserialize_UnknownTransport_IsUdp()
        {
            const string xml =
                "<Receiver><Name>X</Name><Port>1</Port><IsActive>true</IsActive>" +
                "<Encoding>UTF-8</Encoding><ColorString>#FFFFFFFF</ColorString>" +
                "<Transport>Sctp</Transport></Receiver>";

            var receiver = Deserialize(xml);

            Assert.That(receiver.Transport, Is.EqualTo(ReceiverTransport.Udp));
        }

        [Test]
        public void Deserialize_Tcp_IsTcp()
        {
            const string xml =
                "<Receiver><Name>TCP Receiver</Name><Port>4505</Port><IsActive>true</IsActive>" +
                "<Encoding>UTF-8</Encoding><ColorString>#FFFFFFFF</ColorString>" +
                "<Transport>Tcp</Transport></Receiver>";

            var receiver = Deserialize(xml);

            Assert.That(receiver.Transport, Is.EqualTo(ReceiverTransport.Tcp));
            Assert.That(receiver.Port, Is.EqualTo(4505));
        }

        private static Receiver Deserialize(string xml)
        {
            var ser = new XmlSerializer(typeof(Receiver));
            using (var reader = new StringReader(xml))
                return (Receiver)ser.Deserialize(reader);
        }
    }
}
