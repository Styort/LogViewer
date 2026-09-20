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
    public class UdpLogSource : INetworkLogSource
    {
        private UdpClient _udpClient;
        private IPEndPoint _remoteIpEndPoint;
        private readonly IReceiverConfig _config;
        private readonly ILogParser _parser;
        private readonly IEnumerable<string> _ignoredIps;
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
            _ignoredIps = ignoredIps ?? Array.Empty<string>();
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
                _udpClient = new UdpClient(_config.Port);
                _remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
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

            _running = true;
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _udpClient?.Close();
                _udpClient = null;
            }
            catch { }
        }

        private void ReceiveLoop()
        {
            var encoding = Encoding.GetEncoding(_config.Encoding ?? "UTF-8");
            while (_running && _udpClient != null)
            {
                try
                {
                    byte[] receiveBytes = _udpClient.Receive(ref _remoteIpEndPoint);
                    string remoteAddress = _remoteIpEndPoint.Address.ToString();
                    if (_ignoredIps.Any(ip => !string.IsNullOrEmpty(ip) && remoteAddress.IndexOf(ip, StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

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
                    // ignore receive errors when stopping
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
