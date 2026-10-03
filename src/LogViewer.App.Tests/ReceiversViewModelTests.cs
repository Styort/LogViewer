using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LogViewer.Adapters;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Factories;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ReceiversViewModelTests
    {
        [Test]
        public void Recreate_WhileRunning_KeepsReceiving()
        {
            // "Ignore this IP" recreates the sockets; receive used to stay stopped while the UI showed "running".
            var env = Create(GetFreeUdpPort());
            try
            {
                env.Receivers.Start();
                env.Receivers.RecreateUdpSources();

                Assert.That(env.Receivers.StartIsEnabled, Is.False);
                SendEvent(env.Port, "after-recreate");
                Assert.That(() => env.HasReceived("after-recreate"), Is.True.After(3000, 50));
            }
            finally
            {
                env.Receivers.Pause();
            }
        }

        [Test]
        public void Recreate_WhilePaused_StaysPaused()
        {
            var env = Create(GetFreeUdpPort());
            try
            {
                env.Receivers.RecreateUdpSources();

                Assert.That(env.Receivers.StartIsEnabled, Is.True);
            }
            finally
            {
                env.Receivers.Pause();
            }
        }

        [Test]
        public void Recreate_NoActiveReceivers_DropsOldSourcesAndShowsStopped()
        {
            var env = Create(GetFreeUdpPort());
            env.Receivers.Start();
            env.Settings.Receivers[0].IsActive = false;

            env.Receivers.RecreateUdpSources();

            Assert.That(env.Receivers.NetworkSources, Is.Empty);
            Assert.That(env.Receivers.StartIsEnabled, Is.True);
        }

        [Test]
        public void Recreate_KeepsFileFollowSourcesSubscribed()
        {
            var env = Create(GetFreeUdpPort());
            var file = new FakeSource();
            env.Processing.AddSource(file);
            try
            {
                env.Receivers.RecreateUdpSources();

                file.Raise(new LogEntry { Logger = "from-file", Address = "app.log", Level = LogLevel.Info, Message = "m", Time = DateTime.Now });
                Assert.That(env.HasReceived("from-file"), Is.True);
                Assert.That(file.StopCount, Is.Zero, "receiver restart must not stop file follow");
            }
            finally
            {
                env.Receivers.Pause();
            }
        }

        private static ReceiversEnv Create(int port)
        {
            var session = new LogSession();
            var processing = new LogProcessingService(session, new LogFilter());
            var coordinator = new FilterCoordinator(processing, new LoggerFilterState());
            var settings = new FakeAppSettings();
            settings.Receivers.Add(new Receiver { Port = port, Transport = ReceiverTransport.Udp });
            var adapter = new CoreToUiAdapter(new SynchronizationContext(), processing);
            var env = new ReceiversEnv { Port = port, Processing = processing, Settings = settings };
            processing.EntryProcessed += (s, e) =>
            {
                lock (env.ReceivedLoggers)
                    env.ReceivedLoggers.Add(e.Entry.Logger);
            };
            env.Receivers = new ReceiversViewModel(processing, adapter, coordinator, new LogSourceFactory(settings), new FakeDialogs(), settings.Receivers);
            return env;
        }

        private static void SendEvent(int port, string logger)
        {
            string xml = "<log4j:event logger=\"" + logger + "\" level=\"INFO\" timestamp=\"1234567890000\" thread=\"1\">" +
                         "<log4j:message>hi</log4j:message></log4j:event>";
            byte[] bytes = Encoding.UTF8.GetBytes(xml);
            using (var sender = new UdpClient())
                sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, port));
        }

        private static int GetFreeUdpPort()
        {
            using (var probe = new UdpClient(0))
                return ((IPEndPoint)probe.Client.LocalEndPoint).Port;
        }

        private sealed class ReceiversEnv
        {
            public int Port;
            public LogProcessingService Processing;
            public FakeAppSettings Settings;
            public ReceiversViewModel Receivers;
            public readonly System.Collections.Generic.HashSet<string> ReceivedLoggers = new System.Collections.Generic.HashSet<string>();

            public bool HasReceived(string logger)
            {
                lock (ReceivedLoggers)
                    return ReceivedLoggers.Contains(logger);
            }
        }

        private sealed class FakeSource : ILogSource
        {
            public int StopCount;
            public void Start() { }
            public void Stop() => StopCount++;
            public void Raise(LogEntry entry) => LogReceived?.Invoke(this, new LogEntryReceivedEventArgs { Entry = entry });
            public event EventHandler<LogEntryReceivedEventArgs> LogReceived;
        }
    }
}
