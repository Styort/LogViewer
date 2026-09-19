using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Services;
using LogViewer.MVVM.Models;
using LogViewer.Services;

namespace LogViewer.Factories
{
    /// <summary>
    /// Creates UDP log sources from UI settings and receivers. ViewModel calls this then TryInit/AddSource/StartAllSources.
    /// </summary>
    public class UdpSourceFactory
    {
        private readonly XmlLogParser _xmlLogParser = new XmlLogParser();
        private readonly IAppSettings _settings;

        public UdpSourceFactory(IAppSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// One source per active receiver. Inactive receivers are skipped; caller still owns TryInit.
        /// </summary>
        public IReadOnlyList<UdpSourceCreationResult> CreateFromSettings(IEnumerable<Receiver> receivers)
        {
            if (receivers == null) return new List<UdpSourceCreationResult>();

            var active = receivers.Where(r => r.IsActive).ToList();
            if (!active.Any()) return new List<UdpSourceCreationResult>();

            var ignoredIps = (_settings.IgnoredIPs ?? new List<IgnoredIPAddress>())
                .Where(x => x.IsActive && !string.IsNullOrEmpty(x.IP))
                .Select(x => x.IP)
                .ToList();

            var list = new List<UdpSourceCreationResult>();
            foreach (var receiver in active)
            {
                var config = new Adapters.ReceiverConfigAdapter(receiver);
                var source = new UdpLogSource(config, _xmlLogParser, ignoredIps, _settings.IsSeparateIpLoggersByPort);
                list.Add(new UdpSourceCreationResult { Receiver = receiver, Source = source });
            }
            return list;
        }
    }

    /// <summary>Receiver plus the live <see cref="UdpLogSource"/> after <c>CreateFromSettings</c>.</summary>
    public class UdpSourceCreationResult
    {
        /// <summary>UI receiver (port, color) that the source was built from.</summary>
        public Receiver Receiver { get; set; }

        /// <summary>Not started yet; ViewModel calls TryInit then AddSource.</summary>
        public UdpLogSource Source { get; set; }
    }
}
