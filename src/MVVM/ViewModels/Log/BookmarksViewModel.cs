using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.Services;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Bookmarks hold a reference to <see cref="LogMessage"/>. After filtering the presenter creates new
    /// objects — <see cref="SyncWithAllLogs"/> matches them by fields, not ReferenceEquals.
    /// </summary>
    public sealed class BookmarksViewModel : BaseViewModel, IResettable
    {
        private const int BookmarkColumnWidthValue = 22;
        private readonly LogViewState _state;
        private readonly IDialogService _dialogs;
        private RelayCommand _toggleCommand;
        private RelayCommand _addCommand;
        private RelayCommand _removeCommand;
        private RelayCommand _editCommand;
        private RelayCommand _openCommand;

        public BookmarksViewModel(LogViewState state, IDialogService dialogs)
        {
            _state = state;
            _dialogs = dialogs;
            _state.AllLogsChanged += (sender, args) => SyncWithAllLogs();
            _state.LogsChanged += (sender, args) => SyncWithAllLogs();
        }

        /// <summary>Bookmarks in insertion order. After filtering, Log inside may be a new object.</summary>
        public ObservableCollection<LogBookmark> Bookmarks { get; } = new ObservableCollection<LogBookmark>();

        /// <summary>0 while there are no bookmarks — the icon column takes no space.</summary>
        public double BookmarkColumnWidth => Bookmarks.Count > 0 ? BookmarkColumnWidthValue : 0;

        public RelayCommand ToggleBookmarkCommand => _toggleCommand ?? (_toggleCommand = new RelayCommand(ToggleBookmark));
        public RelayCommand AddBookmarkCommand => _addCommand ?? (_addCommand = new RelayCommand(AddBookmark));
        public RelayCommand RemoveBookmarkCommand => _removeCommand ?? (_removeCommand = new RelayCommand(RemoveBookmark));
        public RelayCommand EditBookmarkCommentCommand => _editCommand ?? (_editCommand = new RelayCommand(EditBookmarkComment));
        public RelayCommand OpenBookmarksCommand => _openCommand ?? (_openCommand = new RelayCommand(OpenBookmarks));

        /// <inheritdoc />
        public void Reset()
        {
            ClearAll();
        }

        /// <summary>
        /// Reattach bookmarks to the new AllLogs. Without this, HasBookmark stays on discarded objects after filtering.
        /// </summary>
        public void SyncWithAllLogs()
        {
            if (Bookmarks.Count == 0)
                return;

            var remaining = new HashSet<LogMessage>(_state.AllLogs);
            var used = new HashSet<LogMessage>();

            for (int i = Bookmarks.Count - 1; i >= 0; i--)
            {
                var bookmark = Bookmarks[i];
                LogMessage match = null;

                if (bookmark.Log != null && remaining.Contains(bookmark.Log) && used.Add(bookmark.Log))
                    match = bookmark.Log;
                else if (bookmark.Log != null)
                {
                    match = _state.AllLogs.FirstOrDefault(m => !used.Contains(m) && SameLogIdentity(m, bookmark.Log));
                    if (match != null)
                        used.Add(match);
                }

                if (match == null)
                {
                    if (bookmark.Log != null)
                        bookmark.Log.HasBookmark = false;
                    Bookmarks.RemoveAt(i);
                    continue;
                }

                if (!ReferenceEquals(bookmark.Log, match) && bookmark.Log != null)
                    bookmark.Log.HasBookmark = false;

                bookmark.Log = match;
                match.HasBookmark = true;
            }

            NotifyChanged();
        }

        private void ToggleBookmark()
        {
            var targets = _state.GetLogsToCopy();
            if (targets.Count == 0)
                return;

            if (targets.All(x => x.HasBookmark))
            {
                foreach (var log in targets)
                    RemoveBookmarkForLog(log);
                return;
            }

            AddBookmarks(targets.Where(x => !x.HasBookmark).ToList());
        }

        private void AddBookmark(object obj)
        {
            var targets = obj is LogMessage log ? new List<LogMessage> { log } : _state.GetLogsToCopy();
            AddBookmarks(targets.Where(x => x != null && !x.HasBookmark).ToList());
        }

        private void RemoveBookmark(object obj)
        {
            if (obj is LogBookmark bookmark)
            {
                RemoveBookmarkItem(bookmark);
                return;
            }

            var targets = obj is LogMessage log ? new List<LogMessage> { log } : _state.GetLogsToCopy();
            foreach (var target in targets)
                RemoveBookmarkForLog(target);
        }

        private void EditBookmarkComment(object obj)
        {
            LogBookmark bookmark = obj as LogBookmark;
            if (bookmark == null)
            {
                var log = obj as LogMessage ?? _state.SelectedLog;
                bookmark = FindBookmark(log);
            }

            if (bookmark == null)
                return;

            if (!_dialogs.TryPromptComment(bookmark.Comment, out var comment))
                return;

            bookmark.Comment = comment;
        }

        private void OpenBookmarks()
        {
            var vm = new BookmarkListViewModel(Bookmarks);
            vm.NavigateToLog += (sender, bookmark) => NavigateToBookmark(bookmark);
            vm.EditCommentRequested += (sender, bookmark) => EditBookmarkComment(bookmark);
            vm.RemoveRequested += (sender, bookmark) => RemoveBookmarkItem(bookmark);
            _dialogs.ShowOrActivateBookmarks(vm);
        }

        private void AddBookmarks(List<LogMessage> targets)
        {
            if (targets == null || targets.Count == 0)
                return;

            if (!_dialogs.TryPromptComment(string.Empty, out var comment))
                return;

            foreach (var log in targets)
            {
                if (log == null || log.HasBookmark || FindBookmark(log) != null)
                    continue;

                Bookmarks.Add(new LogBookmark
                {
                    Log = log,
                    Comment = comment
                });
                log.HasBookmark = true;
            }

            NotifyChanged();
        }

        private void RemoveBookmarkForLog(LogMessage log)
        {
            RemoveBookmarkItem(FindBookmark(log));
        }

        private void RemoveBookmarkItem(LogBookmark bookmark)
        {
            if (bookmark == null)
                return;

            if (bookmark.Log != null)
                bookmark.Log.HasBookmark = false;

            Bookmarks.Remove(bookmark);
            NotifyChanged();
        }

        private LogBookmark FindBookmark(LogMessage log)
        {
            if (log == null)
                return null;
            return Bookmarks.FirstOrDefault(x => x.Log == log);
        }

        private void NavigateToBookmark(LogBookmark bookmark)
        {
            var message = bookmark?.Log;
            if (message == null)
                return;

            if (!_state.Logs.Contains(message) && _state.AllLogs.Contains(message))
                _dialogs.ShowInformation(Locals.BookmarkNotVisible, Locals.Information);

            Application.Current?.MainWindow?.Activate();
            _state.SelectedLog = message;
        }

        private void ClearAll()
        {
            if (Bookmarks.Count == 0)
                return;

            foreach (var bookmark in Bookmarks)
            {
                if (bookmark.Log != null)
                    bookmark.Log.HasBookmark = false;
            }

            Bookmarks.Clear();
            NotifyChanged();
        }

        private static bool SameLogIdentity(LogMessage left, LogMessage right)
        {
            if (left == null || right == null)
                return false;

            // Entries have no stable id; matching by fields is the same contract as logger statistics.
            return left.Time == right.Time
                   && left.Level == right.Level
                   && left.Thread == right.Thread
                   && left.ProcessID == right.ProcessID
                   && left.Logger == right.Logger
                   && left.Address == right.Address
                   && left.Message == right.Message;
        }

        private void NotifyChanged()
        {
            OnPropertyChanged(nameof(Bookmarks));
            OnPropertyChanged(nameof(BookmarkColumnWidth));
        }
    }
}
