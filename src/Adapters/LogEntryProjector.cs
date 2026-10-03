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
    /// <remarks>
    /// Rows share one display <see cref="Receiver"/> snapshot per port + transport instead of a clone per row.
    /// It is a snapshot, not the live settings object, so editing a receiver in the settings window does
    /// not recolor rows before OK; <see cref="LogViewer.MVVM.ViewModels.Log.SettingsChangeApplier"/> and
    /// <see cref="Project"/> push the new color/name into the shared snapshot. UI thread only.
    /// </remarks>
    public sealed class LogEntryProjector
    {
        private readonly IList<Receiver> _receivers;
        private readonly IAppSettings _settings;
        private readonly RowHighlightApplier _highlight;
        private readonly Dictionary<(int Port, ReceiverTransport Transport), Receiver> _displayReceivers =
            new Dictionary<(int Port, ReceiverTransport Transport), Receiver>();

        public LogEntryProjector(IList<Receiver> receivers, IAppSettings settings, RowHighlightApplier highlight = null)
        {
            _receivers = receivers;
            _settings = settings;
            _highlight = highlight;
        }

        /// <summary>
        /// One UI row. Returns null if the converter rejects the entry (should not happen for valid Core data).
        /// </summary>
        public LogMessage Project(LogEntry entry)
        {
            if (entry == null)
                return null;
            var rec = Receiver.Find(_receivers, entry.ReceiverPort, entry.ReceiverTransport);
            var msg = LogEntryConverter.ToLogMessage(entry, GetDisplayReceiver(entry, rec));

            if (_highlight != null)
                _highlight.Apply(msg);
            else if (rec != null && _settings != null && _settings.ShowMessageHighlightByReceiverColor)
            {
                // Keep the wash translucent; opaque Color makes the message text unreadable.
                var mc = rec.Color.Clone();
                mc.Opacity = 0.1;
                msg.ToggleMark = mc;
                msg.RowBackground = mc;
            }

            return msg;
        }

        private Receiver GetDisplayReceiver(LogEntry entry, Receiver live)
        {
            var key = (entry.ReceiverPort, entry.ReceiverTransport);
            if (!_displayReceivers.TryGetValue(key, out var display))
            {
                // Unknown receiver (file import, removed receiver): keep port + transport so the row
                // still converts back to the same LogEntry identity.
                display = live != null
                    ? (Receiver)live.Clone()
                    : new Receiver { Port = entry.ReceiverPort, Transport = entry.ReceiverTransport };
                _displayReceivers[key] = display;
                return display;
            }

            if (live != null)
            {
                if (!ReferenceEquals(display.Color, live.Color))
                    display.Color = live.Color;
                if (display.Name != live.Name)
                    display.Name = live.Name;
            }
            return display;
        }
    }
}
