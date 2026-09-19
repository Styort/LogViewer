using LogViewer.MVVM.Models;

namespace LogViewer.Services.Wpf
{
    /// <summary>Proxy for <c>Settings.Instance</c>. The singleton is left intact; it serializes XML.</summary>
    /// <inheritdoc cref="IAppSettings" />
    public sealed class WpfAppSettings : IAppSettings
    {
        public bool AutoStartInStartup => Settings.Instance.AutoStartInStartup;
        public bool IsEnabledMaxMessageBufferSize => Settings.Instance.IsEnabledMaxMessageBufferSize;
        public int MaxMessageBufferSize => Settings.Instance.MaxMessageBufferSize;
        public int DeletedMessagesCount => Settings.Instance.DeletedMessagesCount;
        public string DataFormat => Settings.Instance.DataFormat;
        public string FontColor => Settings.Instance.FontColor;
        public string MessageFontFamily => Settings.Instance.MessageFontFamily;
        public double MessageFontSize => Settings.Instance.MessageFontSize;
        public Theme CurrentTheme => Settings.Instance.CurrentTheme;
        public System.Collections.Generic.List<Receiver> Receivers => Settings.Instance.Receivers;
        public System.Collections.Generic.List<IgnoredIPAddress> IgnoredIPs => Settings.Instance.IgnoredIPs;
        public bool IsShowSourceColumn => Settings.Instance.IsShowSourceColumn;
        public bool IsShowThreadColumn => Settings.Instance.IsShowThreadColumn;
        public bool IsShowTaskbarProgress => Settings.Instance.IsShowTaskbarProgress;
        public bool IsShowErrorTimeline => Settings.Instance.IsShowErrorTimeline;
        public bool ShowMessageHighlightByReceiverColor => Settings.Instance.ShowMessageHighlightByReceiverColor;
        public System.Collections.Generic.List<HighlightRuleItem> HighlightRules => Settings.Instance.HighlightRules;
        public bool IsSeparateIpLoggersByPort => Settings.Instance.IsSeparateIpLoggersByPort;
        public bool Save() => Settings.Instance.Save();
    }
}
