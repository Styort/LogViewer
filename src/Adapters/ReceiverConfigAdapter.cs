using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.MVVM.Models;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Wraps UI Receiver as Core IReceiverConfig. No UI types in Core.
    /// </summary>
    public class ReceiverConfigAdapter : IReceiverConfig
    {
        private readonly Receiver _receiver;

        public ReceiverConfigAdapter(Receiver receiver)
        {
            _receiver = receiver ?? throw new System.ArgumentNullException(nameof(receiver));
        }

        public int Port => _receiver.Port;
        public string Encoding => _receiver.Encoding ?? "UTF-8";
        public string Name => _receiver.Name ?? "UDP Receiver";
        public ReceiverTransport Transport => _receiver.Transport;
    }
}
