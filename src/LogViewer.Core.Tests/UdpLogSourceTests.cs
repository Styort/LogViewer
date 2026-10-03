using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class UdpLogSourceTests
    {
        [Test]
        public void StopThenStart_EachDatagramReceivedOnceWithSenderAddress()
        {
            int port = GetFreeUdpPort();
            var source = new UdpLogSource(new StubConfig(port, "UTF-8"), new XmlLogParser(), new string[0]);
            var received = new ConcurrentBag<LogEntry>();
            source.LogReceived += (s, e) => received.Add(e.Entry);

            string err;
            Assert.That(source.TryInit(out err), Is.True, err);
            source.Start();
            // The first receive thread is still blocked in Receive when Start creates the second one.
            source.Stop();
            source.Start();
            try
            {
                const int count = 50;
                using (var sender = new UdpClient())
                {
                    for (int i = 0; i < count; i++)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(EventXml("L" + i));
                        sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, port));
                    }
                }

                Assert.That(() => received.Count, Is.EqualTo(count).After(3000, 50));
                Assert.That(received.Select(e => e.Logger).Distinct().Count(), Is.EqualTo(count), "no datagram twice");
                Assert.That(received, Has.All.Matches<LogEntry>(e => e.Address == "127.0.0.1"));
            }
            finally
            {
                source.Stop();
            }
        }

        [Test]
        public void TryInit_UnknownEncoding_ReturnsFalseInsteadOfCrashingTheReceiveThread()
        {
            int port = GetFreeUdpPort();
            var source = new UdpLogSource(new StubConfig(port, "no-such-encoding"), new XmlLogParser(), new string[0]);
            try
            {
                string err;
                Assert.That(source.TryInit(out err), Is.False);
                Assert.That(err, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                source.Stop();
            }
        }

        private static string EventXml(string logger)
        {
            return "<log4j:event logger=\"" + logger + "\" level=\"INFO\" timestamp=\"1234567890000\" thread=\"1\">" +
                   "<log4j:message>hi</log4j:message></log4j:event>";
        }

        private static int GetFreeUdpPort()
        {
            using (var probe = new UdpClient(0))
                return ((IPEndPoint)probe.Client.LocalEndPoint).Port;
        }

        private sealed class StubConfig : IReceiverConfig
        {
            public StubConfig(int port, string encoding)
            {
                Port = port;
                Encoding = encoding;
            }

            public int Port { get; }
            public string Encoding { get; }
            public string Name => "UDP Receiver";
            public ReceiverTransport Transport => ReceiverTransport.Udp;
        }
    }
}
