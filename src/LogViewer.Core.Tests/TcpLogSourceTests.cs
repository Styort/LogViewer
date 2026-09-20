using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using NUnit.Framework;

namespace LogViewer.Core.Tests
{
    [TestFixture]
    public class TcpLogSourceTests
    {
        [Test]
        public void TryInit_PortBusy_ReturnsFalse()
        {
            int port = GetFreePort();
            var first = new TcpLogSource(new StubConfig(port), new XmlLogParser(), new string[0]);
            var second = new TcpLogSource(new StubConfig(port), new XmlLogParser(), new string[0]);
            try
            {
                string err;
                Assert.That(first.TryInit(out err), Is.True);
                Assert.That(second.TryInit(out err), Is.False);
                Assert.That(err, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                first.Stop();
                second.Stop();
            }
        }

        [Test]
        public void TwoClients_SplitFrame_BothEventsReceived()
        {
            int port = GetFreePort();
            var source = new TcpLogSource(new StubConfig(port), new XmlLogParser(), new string[0]);
            var received = new ConcurrentBag<LogEntry>();
            source.LogReceived += (s, e) => received.Add(e.Entry);

            string err;
            Assert.That(source.TryInit(out err), Is.True);
            source.Start();
            try
            {
                string eventA = EventXml("ClientA");
                string eventB = EventXml("ClientB");
                SendSplit(port, eventA);
                SendWhole(port, eventB);

                Assert.That(() => received.Count, Is.EqualTo(2).After(3000, 50));
                Assert.That(received, Has.Some.Matches<LogEntry>(e => e.Logger == "ClientA"));
                Assert.That(received, Has.Some.Matches<LogEntry>(e => e.Logger == "ClientB"));
                Assert.That(received, Has.All.Matches<LogEntry>(e => e.ReceiverPort == port));
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

        private static void SendWhole(int port, string xml)
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, port);
                byte[] data = Encoding.UTF8.GetBytes(xml);
                NetworkStream stream = client.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Flush();
                Thread.Sleep(50);
            }
        }

        private static void SendSplit(int port, string xml)
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, port);
                byte[] data = Encoding.UTF8.GetBytes(xml);
                int mid = Math.Max(1, data.Length / 2);
                NetworkStream stream = client.GetStream();
                stream.Write(data, 0, mid);
                stream.Flush();
                Thread.Sleep(30);
                stream.Write(data, mid, data.Length - mid);
                stream.Flush();
                Thread.Sleep(50);
            }
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private sealed class StubConfig : IReceiverConfig
        {
            public StubConfig(int port)
            {
                Port = port;
            }

            public int Port { get; }
            public string Encoding => "UTF-8";
            public string Name => "TCP";
            public ReceiverTransport Transport => ReceiverTransport.Tcp;
        }
    }
}
