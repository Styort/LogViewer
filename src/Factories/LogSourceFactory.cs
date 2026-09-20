using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.MVVM.Models;
using LogViewer.Services;

namespace LogViewer.Factories
{
    /// <summary>
    /// Creates UDP or TCP log sources from UI settings. ViewModel calls this then TryInit/AddSource/StartAllSources.
    /// Ignored IPs are resolved once for all receivers.
    /// </summary>
    public class LogSourceFactory
    {
        private readonly XmlLogParser _xmlLogParser = new XmlLogParser();
        private readonly IAppSettings _settings;

        public LogSourceFactory(IAppSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// One source per active receiver. Inactive receivers are skipped; caller still owns TryInit.
        /// </summary>
        public IReadOnlyList<LogSourceCreationResult> CreateFromSettings(IEnumerable<Receiver> receivers)
        {
            if (receivers == null) return new List<LogSourceCreationResult>();

            var active = receivers.Where(r => r.IsActive).ToList();
            if (!active.Any()) return new List<LogSourceCreationResult>();

            var ignoredIps = (_settings.IgnoredIPs ?? new List<IgnoredIPAddress>())
                .Where(x => x.IsActive && !string.IsNullOrEmpty(x.IP))
                .Select(x => x.IP)
                .ToList();

            var list = new List<LogSourceCreationResult>();
            foreach (var receiver in active)
            {
                var config = new Adapters.ReceiverConfigAdapter(receiver);
                INetworkLogSource source;
                if (receiver.Transport == ReceiverTransport.Tcp)
                    source = new TcpLogSource(config, _xmlLogParser, ignoredIps, _settings.IsSeparateIpLoggersByPort);
                else
                    source = new UdpLogSource(config, _xmlLogParser, ignoredIps, _settings.IsSeparateIpLoggersByPort);

                list.Add(new LogSourceCreationResult { Receiver = receiver, Source = source });
            }
            return list;
        }
    }

    /// <summary>Receiver plus the live network source after <c>CreateFromSettings</c>.</summary>
    public class LogSourceCreationResult
    {
        /// <summary>UI receiver (port, color, transport) that the source was built from.</summary>
        public Receiver Receiver { get; set; }

        /// <summary>Not started yet; ViewModel calls TryInit then AddSource.</summary>
        public INetworkLogSource Source { get; set; }
    }
}
