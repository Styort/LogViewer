using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Services;
using LogViewer.MVVM.Models;

namespace LogViewer.Factories
{
    /// <summary>
    /// Creates UDP log sources from UI settings and receivers. ViewModel calls this then TryInit/AddSource/StartAllSources.
    /// </summary>
    public class UdpSourceFactory
    {
        private readonly XmlLogParser _xmlLogParser = new XmlLogParser();

        /// <summary>
        /// Creates UDP sources for all active receivers. Caller is responsible for TryInit and AddSource.
        /// </summary>
        public IReadOnlyList<UdpSourceCreationResult> CreateFromSettings(IEnumerable<Receiver> receivers)
        {
            if (receivers == null) return new List<UdpSourceCreationResult>();

            var active = receivers.Where(r => r.IsActive).ToList();
            if (!active.Any()) return new List<UdpSourceCreationResult>();

            var ignoredIps = (Settings.Instance.IgnoredIPs ?? new List<IgnoredIPAddress>())
                .Where(x => x.IsActive && !string.IsNullOrEmpty(x.IP))
                .Select(x => x.IP)
                .ToList();

            var list = new List<UdpSourceCreationResult>();
            foreach (var receiver in active)
            {
                var config = new Adapters.ReceiverConfigAdapter(receiver);
                var source = new UdpLogSource(config, _xmlLogParser, ignoredIps, Settings.Instance.IsSeparateIpLoggersByPort);
                list.Add(new UdpSourceCreationResult { Receiver = receiver, Source = source });
            }
            return list;
        }
    }

    public class UdpSourceCreationResult
    {
        public Receiver Receiver { get; set; }
        public UdpLogSource Source { get; set; }
    }
}
