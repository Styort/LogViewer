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
        private readonly MessageGroupsViewModel _groups;
        private readonly Action<bool> _setCleanEnabled;

        public LogSessionPresenter(
            LogViewState state,
            LogEntryProjector projector,
            LoggerTreeViewModel tree,
            BookmarksViewModel bookmarks,
            ErrorTimelineViewModel timeline,
            MessageGroupsViewModel groups,
            Action<bool> setCleanEnabled)
        {
            _state = state;
            _projector = projector;
            _tree = tree;
            _bookmarks = bookmarks;
            _timeline = timeline;
            _groups = groups;
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
            _groups.ScheduleRebuild();
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
            _groups.Reset();
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
            _timeline.ScheduleRebuild();
            _groups.ScheduleRebuild();
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
                _tree.TryApplyQueuedDisplayRestore();
                _tree.RememberPathsFromTree();
                _tree.SyncCheckboxesFromExclusions();
                _bookmarks.TryApplyQueuedRestore();
            }
            else
            {
                // Reuse AllLogs instances (same order subsequence). A new Project() would break
                // bookmark navigation: Logs.Contains uses reference equality.
                filtered = MapFilteredToAllLogs(e.Entries);
            }

            _state.Logs = new AsyncObservableCollection<LogMessage>(filtered);
            _tree.SyncCheckboxesFromExclusions();
            _setCleanEnabled(_state.AllLogs.Any());
            if (_tree.TreeCheckJustDone)
            {
                // Tree checkbox: keep a nearby row. Otherwise ListView jumps to the first visible item.
                _tree.TreeCheckJustDone = false;
                _state.SelectedLog = _state.GetLastSelectedOrNearby();
            }
        }

        /// <summary>
        /// Filtered Core entries are a subsequence of the session buffer. Walk AllLogs once and keep
        /// the same LogMessage objects the bookmarks already hold.
        /// </summary>
        private List<LogMessage> MapFilteredToAllLogs(IReadOnlyList<LogEntry> entries)
        {
            var filtered = new List<LogMessage>(entries.Count);
            var all = _state.AllLogs;
            int iAll = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;
                while (iAll < all.Count && !Matches(all[iAll], entry))
                    iAll++;
                if (iAll < all.Count)
                {
                    filtered.Add(all[iAll]);
                    iAll++;
                    continue;
                }

                var msg = _projector.Project(entry);
                if (msg != null)
                    filtered.Add(msg);
            }

            return filtered;
        }

        private static bool Matches(LogMessage message, LogEntry entry)
        {
            if (message == null || entry == null)
                return false;
            return message.Time == entry.Time
                   && (int)message.Level == (int)entry.Level
                   && message.Thread == entry.Thread
                   && message.ProcessID == entry.ProcessID
                   && message.Logger == entry.Logger
                   && message.Address == entry.Address
                   && message.Message == entry.Message;
        }
    }
}
