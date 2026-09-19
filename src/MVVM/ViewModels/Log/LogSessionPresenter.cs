using System;
using System.Collections.Generic;
using System.Linq;
using LogViewer.Adapters;
using LogViewer.Core.Domain;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Core adapter events → <see cref="LogViewState"/> and the tree.
    /// Extracted from the host so LogViewModel does not grow batch handlers.
    /// </summary>
    public sealed class LogSessionPresenter
    {
        private readonly LogViewState _state;
        private readonly LogEntryProjector _projector;
        private readonly LoggerTreeViewModel _tree;
        private readonly BookmarksViewModel _bookmarks;
        private readonly ErrorTimelineViewModel _timeline;
        private readonly Action<bool> _setCleanEnabled;

        public LogSessionPresenter(
            LogViewState state,
            LogEntryProjector projector,
            LoggerTreeViewModel tree,
            BookmarksViewModel bookmarks,
            ErrorTimelineViewModel timeline,
            Action<bool> setCleanEnabled)
        {
            _state = state;
            _projector = projector;
            _tree = tree;
            _bookmarks = bookmarks;
            _timeline = timeline;
            _setCleanEnabled = setCleanEnabled;
        }

        /// <summary>A batch from the receive thread is already on the UI: add to AllLogs/Logs and extend the tree.</summary>
        public void OnEntriesProcessed(object sender, LogEntriesProcessedEventArgs e)
        {
            if (e?.Entries == null || e.Entries.Count == 0) return;

            for (int i = 0; i < e.Entries.Count; i++)
            {
                var item = e.Entries[i];
                if (item?.Entry == null) continue;
                var msg = _projector.Project(item.Entry);
                if (msg == null) continue;
                _state.AllLogs.Add(msg);
                if (item.IncludedInFilter)
                    _state.Logs.Add(msg);
                _tree.BuildTreeByMessage(msg);
            }

            _setCleanEnabled(_state.AllLogs.Any());
            _timeline.ScheduleRebuild();
        }

        /// <summary>Session is empty in Core: lists, tree, bookmarks, and timeline in the same frame.</summary>
        public void OnSessionCleared(object sender, EventArgs e)
        {
            _bookmarks.Reset();
            _state.AllLogs.Clear();
            _state.Logs.Clear();
            _tree.OnSessionCleared();
            _setCleanEnabled(false);
            _timeline.Rebuild();
        }

        /// <summary>Max-buffer trim: drop count entries from the head of both lists, keep the selection anchor.</summary>
        public void OnEntriesRemoved(int count)
        {
            if (count <= 0) return;
            // Max buffer: Core drops old entries from the head; the anchor is needed or selection becomes null.
            _state.CaptureSelectionAnchor();
            var allList = _state.AllLogs.ToList();
            var logsList = _state.Logs.ToList();
            int removeAll = Math.Min(count, allList.Count);
            if (removeAll > 0) allList.RemoveRange(0, removeAll);
            int removeLogs = Math.Min(count, logsList.Count);
            if (removeLogs > 0) logsList.RemoveRange(0, removeLogs);
            _state.AllLogs = new AsyncObservableCollection<LogMessage>(allList);
            _state.Logs = new AsyncObservableCollection<LogMessage>(logsList);
            _state.SelectedLog = _state.GetLastSelectedOrNearby();
        }

        /// <summary>
        /// New filtered view. AllEntries != null also replaces the full buffer (Don't Receive dropped entries).
        /// </summary>
        public void OnFilteredViewUpdated(object sender, FilteredViewUpdatedEventArgs e)
        {
            if (e?.Entries == null) return;

            List<LogMessage> filtered;
            if (e.AllEntries != null)
            {
                // Full AllLogs rebuild: Don't Receive dropped entries from the session,
                // re-project so the UI does not keep ghost rows.
                var all = new List<LogMessage>(e.AllEntries.Count);
                var map = new Dictionary<LogEntry, LogMessage>(e.AllEntries.Count);
                for (int i = 0; i < e.AllEntries.Count; i++)
                {
                    var entry = e.AllEntries[i];
                    var msg = _projector.Project(entry);
                    if (msg == null) continue;
                    all.Add(msg);
                    map[entry] = msg;
                }
                filtered = new List<LogMessage>(e.Entries.Count);
                for (int i = 0; i < e.Entries.Count; i++)
                {
                    var entry = e.Entries[i];
                    if (map.TryGetValue(entry, out var msg))
                        filtered.Add(msg);
                    else
                    {
                        msg = _projector.Project(entry);
                        if (msg != null) filtered.Add(msg);
                    }
                }
                _state.AllLogs = new AsyncObservableCollection<LogMessage>(all);
                _tree.RebuildFromCore();
            }
            else
            {
                filtered = new List<LogMessage>(e.Entries.Count);
                for (int i = 0; i < e.Entries.Count; i++)
                {
                    var msg = _projector.Project(e.Entries[i]);
                    if (msg != null) filtered.Add(msg);
                }
            }

            _state.Logs = new AsyncObservableCollection<LogMessage>(filtered);
            _setCleanEnabled(_state.AllLogs.Any());
            if (_tree.TreeCheckJustDone)
            {
                // Tree checkbox: keep a nearby row. Otherwise ListView jumps to the first visible item.
                _tree.TreeCheckJustDone = false;
                _state.SelectedLog = _state.GetLastSelectedOrNearby();
            }
        }
    }
}
