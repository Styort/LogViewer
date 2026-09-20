using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Live log source over TCP (NLogViewer / log4net Chainsaw).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="UdpLogSource"/>, TCP is not one-event-per-datagram. A <see cref="TcpListener"/>
    /// accepts any number of clients on the receiver port; each connection is read on its own
    /// background thread. Bytes are decoded with the receiver encoding (a <see cref="Decoder"/>
    /// so a UTF-8 character split across segments is not corrupted), then passed through
    /// <see cref="Log4jXmlFrameSplitter"/>. Complete frames go to <see cref="XmlLogParser"/> and
    /// raise <see cref="LogReceived"/> on the worker thread — the same path as UDP into
    /// <c>LogProcessingService</c> / UI batching.
    ///
    /// <see cref="Stop"/> closes the listener and every client. The UI thread must not Join.
    /// </remarks>
    public class TcpLogSource : INetworkLogSource
    {
        private readonly IReceiverConfig _config;
        private readonly ILogParser _parser;
        private readonly IEnumerable<string> _ignoredIps;
        private readonly bool _separateAddressByPort;
        private readonly object _clientsLock = new object();
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private TcpListener _listener;
        private volatile bool _running;
        private Thread _acceptThread;

        public TcpLogSource(
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

        public bool TryInit(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                _listener = new TcpListener(IPAddress.Any, _config.Port);
                _listener.Start();
                return true;
            }
            catch (SocketException ex)
            {
                errorMessage = ex.Message;
                _listener = null;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _listener = null;
                return false;
            }
        }

        public void Start()
        {
            if (_listener == null)
            {
                string unused;
                if (!TryInit(out unused))
                    return;
            }

            if (_running)
                return;

            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            _listener = null;

            TcpClient[] snapshot;
            lock (_clientsLock)
            {
                snapshot = _clients.ToArray();
                _clients.Clear();
            }

            foreach (var client in snapshot)
                CloseClient(client);
        }

        private void AcceptLoop()
        {
            TcpListener listener = _listener;
            while (_running && listener != null && ReferenceEquals(_listener, listener))
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    string remoteAddress = GetRemoteAddress(client);
                    if (IsIgnored(remoteAddress))
                    {
                        CloseClient(client);
                        continue;
                    }

                    lock (_clientsLock)
                    {
                        if (!_running || !ReferenceEquals(_listener, listener))
                        {
                            CloseClient(client);
                            return;
                        }

                        _clients.Add(client);
                    }

                    var thread = new Thread(() => ReadClient(client, remoteAddress)) { IsBackground = true };
                    thread.Start();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (!_running || !ReferenceEquals(_listener, listener))
                        break;
                }
                catch (Exception)
                {
                    if (!_running || !ReferenceEquals(_listener, listener))
                        break;
                }
            }
        }

        private void ReadClient(TcpClient client, string remoteAddress)
        {
            var encoding = Encoding.GetEncoding(_config.Encoding ?? "UTF-8");
            var decoder = encoding.GetDecoder();
            var splitter = new Log4jXmlFrameSplitter();
            var bytes = new byte[4096];
            var chars = new char[encoding.GetMaxCharCount(bytes.Length)];
            string address = _separateAddressByPort ? $"{remoteAddress}:{_config.Port}" : remoteAddress;

            try
            {
                NetworkStream stream = client.GetStream();
                while (_running)
                {
                    int n;
                    try
                    {
                        n = stream.Read(bytes, 0, bytes.Length);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (System.IO.IOException)
                    {
                        break;
                    }

                    if (n <= 0)
                        break;

                    int charCount = decoder.GetChars(bytes, 0, n, chars, 0);
                    if (charCount <= 0)
                        continue;

                    IReadOnlyList<string> frames = splitter.Append(new string(chars, 0, charCount));
                    if (splitter.ConsumeOverflow())
                        Publish(CreateOverflowEntry(address));

                    foreach (string frame in frames)
                    {
                        if (string.IsNullOrWhiteSpace(frame))
                            continue;
                        try
                        {
                            Publish(LiveXmlLogEntryFactory.Create(
                                _parser, frame, address, _config.Port, ReceiverTransport.Tcp, "TCP Logger"));
                        }
                        catch (Exception)
                        {
                            // Factory already turns parse failures into Error rows.
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                lock (_clientsLock)
                    _clients.Remove(client);
                CloseClient(client);
            }
        }

        private LogEntry CreateOverflowEntry(string address)
        {
            return new LogEntry
            {
                Logger = "TCP Logger",
                Address = address,
                ReceiverPort = _config.Port,
                ReceiverTransport = ReceiverTransport.Tcp,
                Thread = -1,
                Message = $"TCP XML buffer exceeded {Log4jXmlFrameSplitter.DefaultMaxBufferChars} characters; dropped incomplete data.",
                Time = DateTime.Now,
                Level = LogLevel.Error,
                ExecutableName = "LogViewer"
            };
        }

        private void Publish(LogEntry entry)
        {
            LogReceived?.Invoke(this, new LogEntryReceivedEventArgs { Entry = entry });
        }

        private bool IsIgnored(string remoteAddress)
        {
            return _ignoredIps.Any(ip =>
                !string.IsNullOrEmpty(ip) &&
                remoteAddress.IndexOf(ip, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetRemoteAddress(TcpClient client)
        {
            try
            {
                var ep = client.Client?.RemoteEndPoint as IPEndPoint;
                return ep != null ? ep.Address.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void CloseClient(TcpClient client)
        {
            try
            {
                client?.Close();
            }
            catch
            {
            }
        }

        public event EventHandler<LogEntryReceivedEventArgs> LogReceived;
    }
}
