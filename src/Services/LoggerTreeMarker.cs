using System;
using System.Linq;
using System.Windows.Media;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.TreeView;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;

namespace LogViewer.Services
{
    /// <summary>
    /// Color marks for loggers in the tree and in list rows.
    /// The counter avoids walking AllLogs on every packet when no marks are set.
    /// </summary>
    public sealed class LoggerTreeMarker
    {
        private const string TransparentColor = "#00FFFFFF";
        private readonly LogViewState _state;
        private readonly IAppSettings _settings;
        private int _toggledMarksCount;

        public LoggerTreeMarker(LogViewState state, IAppSettings settings)
        {
            _state = state;
            _settings = settings;
        }

        /// <summary>How many marks are currently set. 0 — ApplyExistingMark is a no-op on the hot path.</summary>
        public int ToggledMarksCount => _toggledMarksCount;

        /// <summary>Tree Clean: reset the counter, otherwise new rows get a ghost mark.</summary>
        public void ResetCount()
        {
            _toggledMarksCount = 0;
        }

        /// <summary>Toggle the mark on the node and every row whose FullPath contains the node's Logger.</summary>
        public void Toggle(Node node)
        {
            if (node == null)
                return;

            var receiverColor = _state.AllLogs.FirstOrDefault(x => x.FullPath == node.Logger)?.Receiver?.Color?.Clone();
            bool isSet = false;
            SolidColorBrush currentColor;
            if (node.ToggleMark.Color.ToString() != TransparentColor)
            {
                _toggledMarksCount--;
                if (_settings.ShowMessageHighlightByReceiverColor)
                {
                    if (receiverColor != null)
                    {
                        receiverColor.Opacity = 0.3;
                        currentColor = receiverColor;
                    }
                    else
                        currentColor = new SolidColorBrush(Colors.Transparent);
                }
                else
                    currentColor = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                isSet = true;
                _toggledMarksCount++;
                var rnd = new Random();
                Color randomColor = Color.FromRgb((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
                currentColor = new SolidColorBrush(randomColor);
                currentColor.Opacity = 0.1;
            }

            node.ToggleMark = isSet ? currentColor : new SolidColorBrush(Colors.Transparent);
            // Contains, not equality: a mark on a parent must color descendants (A.B when A is marked).
            foreach (var logMessage in _state.Logs.Where(x => x.FullPath.Contains(node.Logger)))
                logMessage.ToggleMark = currentColor;
            foreach (var logMessage in _state.AllLogs.Where(x => x.FullPath.Contains(node.Logger)))
                logMessage.ToggleMark = currentColor;
        }

        /// <summary>A new list row inherits an existing node mark without another Toggle.</summary>
        public void ApplyExistingMark(LogMessage log, Node node)
        {
            if (_toggledMarksCount > 0 && node != null && log != null)
                log.ToggleMark = node.ToggleMark;
        }
    }
}
