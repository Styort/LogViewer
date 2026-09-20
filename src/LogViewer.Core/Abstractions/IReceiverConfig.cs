using LogViewer.Core.Domain;

namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// Minimal config for a live UDP/TCP receiver. No UI. UI implements this from Settings.
    /// </summary>
    public interface IReceiverConfig
    {
        int Port { get; }
        string Encoding { get; }
        string Name { get; }
        /// <summary>Missing from old settings.xml means <see cref="ReceiverTransport.Udp"/>.</summary>
        ReceiverTransport Transport { get; }
    }
}
