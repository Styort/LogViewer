using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Domain;
using LogViewer.MVVM.Models;
using LogViewer.Services;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Projects Core <see cref="LogEntry"/> to UI <see cref="LogMessage"/> with the current receiver color/name.
    /// Color comes from the live Receivers list, not from what the parser stored on the entry:
    /// the user may have renamed the receiver in settings after the packet arrived.
    /// </summary>
    public sealed class LogEntryProjector
    {
        private readonly IList<Receiver> _receivers;
        private readonly IAppSettings _settings;

        public LogEntryProjector(IList<Receiver> receivers, IAppSettings settings)
        {
            _receivers = receivers;
            _settings = settings;
        }

        /// <summary>
        /// One UI row. Returns null if the converter rejects the entry (should not happen for valid Core data).
        /// </summary>
        public LogMessage Project(LogEntry entry)
        {
            var msg = LogEntryConverter.ToLogMessage(entry, _receivers);
            if (msg == null)
                return null;
            var rec = _receivers?.FirstOrDefault(x => x.Port == entry.ReceiverPort);
            if (rec != null)
            {
                msg.Receiver.Color = rec.Color;
                msg.Receiver.Name = rec.Name;
                if (_settings != null && _settings.ShowMessageHighlightByReceiverColor)
                {
                    // Keep the wash translucent; opaque Color makes the message text unreadable.
                    var mc = rec.Color.Clone();
                    mc.Opacity = 0.1;
                    msg.ToggleMark = mc;
                }
            }
            return msg;
        }
    }
}
