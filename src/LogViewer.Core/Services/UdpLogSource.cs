using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.Services
{
    /// <remarks>
    /// Stop closes the socket, which wakes the blocked <c>Receive</c> with an exception. Start may run before
    /// that thread has noticed, so each receive thread owns its socket and remote endpoint as locals and exits
    /// as soon as <c>_udpClient</c> no longer refers to its socket. With shared fields a quick
    /// Stop → Start left two threads reading one socket and writing one endpoint, so a datagram could be
    /// attributed to another sender.
    /// </remarks>
    public class UdpLogSource : INetworkLogSource
    {
        // The default Windows buffer (64 KB) overflows within milliseconds of a burst if this thread is
        // briefly delayed (GC, buffer trim); overflowing datagrams are dropped silently by the OS.
        internal const int ReceiveBufferBytes = 4 * 1024 * 1024;

        private volatile UdpClient _udpClient;
        private Encoding _encoding;
        private readonly IReceiverConfig _config;
        private readonly ILogParser _parser;
        private readonly IgnoredAddressSet _ignoredIps;
        private readonly bool _separateAddressByPort;
        private volatile bool _running;
        private Thread _receiveThread;

        public UdpLogSource(
            IReceiverConfig config,
            ILogParser parser,
            IEnumerable<string> ignoredIps,
            bool separateAddressByPort = false)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _ignoredIps = new IgnoredAddressSet(ignoredIps);
            _separateAddressByPort = separateAddressByPort;
        }

        /// <summary>
        /// Result of Init(). UI can show ErrorMessage if Success is false.
        /// </summary>
        public bool TryInit(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                // Validated here, not in the receive thread: an unknown name from a hand-edited settings.xml
                // threw on a background thread and terminated the process.
                _encoding = Encoding.GetEncoding(string.IsNullOrEmpty(_config.Encoding) ? "UTF-8" : _config.Encoding);
                var client = new UdpClient(_config.Port);
                try
                {
                    client.Client.ReceiveBufferSize = ReceiveBufferBytes;
                }
                catch (SocketException)
                {
                    // Not fatal: the OS may cap the size; receive still works with the default buffer.
                }
                _udpClient = client;
                return true;
            }
            catch (SocketException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public void Start()
        {
            if (_udpClient == null)
            {
                string unused;
                if (!TryInit(out unused))
                    return;
            }

            if (_running)
                return;

            var client = _udpClient;
            var encoding = _encoding;
            if (client == null || encoding == null)
                return;

            _running = true;
            _receiveThread = new Thread(() => ReceiveLoop(client, encoding)) { IsBackground = true };
            _receiveThread.Start();
        }

        public void Stop()
        {
            _running = false;
            var client = _udpClient;
            _udpClient = null;
            try
            {
                client?.Close();
            }
            catch { }
        }

        private bool IsCurrent(UdpClient client)
        {
            return _running && ReferenceEquals(client, _udpClient);
        }

        private void ReceiveLoop(UdpClient client, Encoding encoding)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (IsCurrent(client))
            {
                try
                {
                    byte[] receiveBytes = client.Receive(ref remoteEndPoint);
                    if (!IsCurrent(client))
                        break;
                    if (_ignoredIps.Contains(remoteEndPoint.Address))
                        continue;
                    string remoteAddress = remoteEndPoint.Address.ToString();

                    string incomingLog = encoding.GetString(receiveBytes);
                    string address = _separateAddressByPort ? $"{remoteAddress}:{_config.Port}" : remoteAddress;

                    LogEntry entry = LiveXmlLogEntryFactory.Create(
                        _parser, incomingLog, address, _config.Port, ReceiverTransport.Udp, "UDP Logger");
                    LogReceived?.Invoke(this, new LogEntryReceivedEventArgs { Entry = entry });
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // Closing the socket in Stop lands here; IsCurrent ends the loop. Otherwise it is a
                    // per-datagram error (for example WSAECONNRESET after an ICMP port unreachable).
                }
                catch (Exception)
                {
                    // log and continue
                }
            }
        }

        public event EventHandler<LogEntryReceivedEventArgs> LogReceived;
    }
}
