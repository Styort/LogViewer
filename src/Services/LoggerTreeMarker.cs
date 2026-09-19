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
        private readonly LogViewer.Adapters.RowHighlightApplier _highlight;
        private int _toggledMarksCount;

        public LoggerTreeMarker(LogViewState state, IAppSettings settings, LogViewer.Adapters.RowHighlightApplier highlight = null)
        {
            _state = state;
            _highlight = highlight;
            _ = settings;
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

            bool isSet = false;
            SolidColorBrush currentColor;
            if (node.ToggleMark.Color.ToString() != TransparentColor)
            {
                _toggledMarksCount--;
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
            // ToggleMark is only the tree mark; row fill is recomputed (rule > mark > receiver).
            foreach (var logMessage in _state.Logs.Where(x => x.FullPath.Contains(node.Logger)))
                SetMark(logMessage, currentColor);
            foreach (var logMessage in _state.AllLogs.Where(x => x.FullPath.Contains(node.Logger)))
                SetMark(logMessage, currentColor);
        }

        /// <summary>A new list row inherits an existing node mark without another Toggle.</summary>
        public void ApplyExistingMark(LogMessage log, Node node)
        {
            if (_toggledMarksCount > 0 && node != null && log != null)
                SetMark(log, node.ToggleMark);
        }

        private void SetMark(LogMessage log, SolidColorBrush mark)
        {
            log.ToggleMark = mark;
            _highlight?.Apply(log);
        }
    }
}
