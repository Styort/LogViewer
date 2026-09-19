using System;
using System.Collections.Generic;
using LogViewer.MVVM.Models;

namespace LogViewer.Services
{
    /// <summary>
    /// Read settings without <c>Settings.Instance</c>.
    /// The singleton is not rewritten: the wrapper lets tests and the factory inject another implementation.
    /// Values match those in <c>settings.xml</c>.
    /// </summary>
    public interface IAppSettings
    {
        /// <summary>Start UDP when the app launches, unless this was a manual start from the tray.</summary>
        bool AutoStartInStartup { get; }

        /// <summary>Whether the session buffer cap is enabled (otherwise the list grows unbounded).</summary>
        bool IsEnabledMaxMessageBufferSize { get; }

        /// <summary>Maximum entries in <c>LogSession</c> when the cap is on.</summary>
        int MaxMessageBufferSize { get; }

        /// <summary>How many oldest entries to drop from the head when the cap is exceeded.</summary>
        int DeletedMessagesCount { get; }

        /// <summary>Date format in the list and timeline tooltips (same as settings).</summary>
        string DataFormat { get; }

        /// <summary>Message-details text color (ARGB string).</summary>
        string FontColor { get; }

        /// <summary>Message column font — typically monospace for logs.</summary>
        string MessageFontFamily { get; }

        /// <summary>Message column font size.</summary>
        double MessageFontSize { get; }

        /// <summary>Current theme (icon color / accent).</summary>
        Theme CurrentTheme { get; }

        /// <summary>
        /// The same list that is serialized to XML. Do not copy: the UDP factory and projector read it live.
        /// </summary>
        List<Receiver> Receivers { get; }

        /// <summary>IPs whose packets are dropped at the socket.</summary>
        List<IgnoredIPAddress> IgnoredIPs { get; }

        /// <summary>Show the Source column (IP/address).</summary>
        bool IsShowSourceColumn { get; }

        /// <summary>Show the Thread column.</summary>
        bool IsShowThreadColumn { get; }

        /// <summary>Progress on the taskbar icon during receive/import.</summary>
        bool IsShowTaskbarProgress { get; }

        /// <summary>Warn/Error/Fatal density strip under the list.</summary>
        bool IsShowErrorTimeline { get; }

        /// <summary>Light row fill with the receiver color (otherwise only the color column).</summary>
        bool ShowMessageHighlightByReceiverColor { get; }

        /// <summary>
        /// Split loggers for the same IP by receiver port. Otherwise two UDP ports merge into one branch.
        /// </summary>
        bool IsSeparateIpLoggersByPort { get; }

        /// <summary>Write XML to disk. false means a serialization error; the UI shows a warning.</summary>
        bool Save();
    }
}
