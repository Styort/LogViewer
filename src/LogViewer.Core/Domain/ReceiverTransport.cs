using System;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Live receiver transport. Default is <see cref="Udp"/> so old settings.xml without this field stay UDP.
    /// </summary>
    public enum ReceiverTransport
    {
        Udp = 0,
        Tcp = 1
    }

    public static class ReceiverTransportHelper
    {
        /// <summary>
        /// Missing, empty, or unknown values map to UDP (settings compatibility).
        /// </summary>
        public static ReceiverTransport ParseOrUdp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ReceiverTransport.Udp;

            ReceiverTransport parsed;
            if (!Enum.TryParse(value.Trim(), true, out parsed) || !Enum.IsDefined(typeof(ReceiverTransport), parsed))
                return ReceiverTransport.Udp;

            return parsed;
        }
    }
}
