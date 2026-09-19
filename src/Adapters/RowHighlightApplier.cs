using System;
using System.Collections.Generic;
using System.Windows.Media;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Row fill for the log ListView. Virtualization requires frozen brushes.
    /// </summary>
    /// <remarks>
    /// Priority: highlight rule, then logger toggle-mark, then receiver wash, then transparent.
    /// Bookmarks stay on <see cref="LogMessage.HasBookmark"/>; this does not replace them.
    /// SearchText is not a rule — in-message search highlighting stays a separate mechanism.
    /// </remarks>
    public sealed class RowHighlightApplier
    {
        private static readonly SolidColorBrush TransparentFrozen = CreateTransparent();

        private readonly HighlightEngine _engine;
        private readonly IAppSettings _settings;
        private readonly Dictionary<string, SolidColorBrush> _brushes = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);

        public RowHighlightApplier(HighlightEngine engine, IAppSettings settings)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _settings = settings;
        }

        public void ReplaceRules(IEnumerable<HighlightRule> rules)
        {
            _engine.ReplaceRules(rules);
            _brushes.Clear();
        }

        public void Apply(LogMessage msg)
        {
            if (msg == null)
                return;

            string ruleArgb = _engine.TryMatch((LogLevel)(int)msg.Level, msg.Logger, msg.Message);
            if (!string.IsNullOrEmpty(ruleArgb))
            {
                msg.RowBackground = GetBrush(ruleArgb);
                return;
            }

            if (msg.ToggleMark != null && msg.ToggleMark.Color.A != 0)
            {
                msg.RowBackground = msg.ToggleMark;
                return;
            }

            if (_settings != null && _settings.ShowMessageHighlightByReceiverColor && msg.Receiver?.Color != null)
            {
                msg.RowBackground = GetWash(msg.Receiver.Color);
                return;
            }

            msg.RowBackground = TransparentFrozen;
        }

        public void RecolorAll(LogViewState state)
        {
            if (state == null)
                return;
            Recolor(state.AllLogs);
            Recolor(state.Logs);
        }

        private void Recolor(IList<LogMessage> logs)
        {
            if (logs == null)
                return;
            for (int i = 0; i < logs.Count; i++)
                Apply(logs[i]);
        }

        private SolidColorBrush GetWash(SolidColorBrush receiverColor)
        {
            string key = receiverColor.ToARGB() + "@0.1";
            SolidColorBrush brush;
            if (_brushes.TryGetValue(key, out brush))
                return brush;
            brush = receiverColor.Clone();
            brush.Opacity = 0.1;
            if (!brush.IsFrozen)
                brush.Freeze();
            _brushes[key] = brush;
            return brush;
        }

        private SolidColorBrush GetBrush(string argb)
        {
            SolidColorBrush brush;
            if (_brushes.TryGetValue(argb, out brush))
                return brush;
            try
            {
                brush = new SolidColorBrush().FromARGB(argb);
            }
            catch (Exception)
            {
                return TransparentFrozen;
            }
            if (!brush.IsFrozen)
                brush.Freeze();
            _brushes[argb] = brush;
            return brush;
        }

        private static SolidColorBrush CreateTransparent()
        {
            var brush = new SolidColorBrush(Colors.Transparent);
            brush.Freeze();
            return brush;
        }
    }
}
