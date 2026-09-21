using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.Services;
using NLog;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Save As / Open for a user-chosen session file. Replaces the buffer (with confirm), never merges.
    /// Distinct from filter presets: this file contains log rows; presets do not.
    /// </summary>
    public sealed class SessionViewModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string FileFilter =
            "LogViewer session (*.lvs;*.lvs.gz)|*.lvs;*.lvs.gz|XML session (*.xml)|*.xml|All files (*.*)|*.*";

        private readonly LogSession _session;
        private readonly LogViewState _state;
        private readonly FilterCoordinator _coordinator;
        private readonly SearchViewModel _search;
        private readonly BookmarksViewModel _bookmarks;
        private readonly LoggerTreeViewModel _tree;
        private readonly IDialogService _dialogs;
        private readonly IFileDialogService _files;
        private readonly ReceiversViewModel _receivers;
        private readonly Action _clean;
        private readonly Action<eLogLevel> _setMinLevelUi;
        private RelayCommand _saveCommand;
        private RelayCommand _openCommand;

        public SessionViewModel(
            LogSession session,
            LogViewState state,
            FilterCoordinator coordinator,
            SearchViewModel search,
            BookmarksViewModel bookmarks,
            LoggerTreeViewModel tree,
            IDialogService dialogs,
            IFileDialogService files,
            ReceiversViewModel receivers,
            Action clean,
            Action<eLogLevel> setMinLevelUi)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _receivers = receivers ?? throw new ArgumentNullException(nameof(receivers));
            _clean = clean ?? throw new ArgumentNullException(nameof(clean));
            _setMinLevelUi = setMinLevelUi ?? throw new ArgumentNullException(nameof(setMinLevelUi));
        }

        public RelayCommand SaveCommand =>
            _saveCommand ?? (_saveCommand = new RelayCommand(_ => Save(), _ => CanSave()));

        public RelayCommand OpenCommand =>
            _openCommand ?? (_openCommand = new RelayCommand(_ => Open()));

        private bool CanSave()
        {
            return _session.EntryCount > 0
                   || _coordinator.Loggers.ExcludedPaths.Count > 0
                   || _coordinator.Loggers.ExcludedWithBufferPaths.Count > 0;
        }

        /// <summary>.lvs / .lvs.gz — not .xml, so ordinary log XML is still imported.</summary>
        public static bool IsSessionFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            return path.EndsWith(".lvs", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(".lvs.gz", StringComparison.OrdinalIgnoreCase);
        }

        private void Save()
        {
            if (_state.IsBusy)
                return;

            int count = _session.EntryCount;
            if (count > SavedSessionDocument.LargeBufferWarningThreshold
                && !_dialogs.Confirm(string.Format(Locals.SessionLargeBufferWarning, count), Locals.Warning))
                return;

            _receivers.Pause();
            var path = _files.SaveFile(".lvs", FileFilter, $"LogViewerSession_{DateTime.Now:yy-MM-dd}");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var document = BuildDocument();
                SavedSessionXmlStore.SaveFile(path, document);
                _files.OpenFolder(Path.GetDirectoryName(path));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save session");
                _dialogs.ShowError(string.Format(Locals.SessionSaveFailed, ex.Message), Locals.Error);
            }
        }

        public void Open()
        {
            if (_state.IsBusy)
                return;

            var path = _files.OpenFile(FileFilter);
            OpenFromPath(path);
        }

        /// <summary>Open from the file dialog, drag-and-drop, or a command-line argument.</summary>
        public async void OpenFromPath(string path)
        {
            await OpenFromPathAsync(path);
        }

        /// <summary>
        /// Parse the file off the UI thread and show the circular loader. Apply still runs on the
        /// dispatcher (WPF collections), after a render pump so the spinner can paint.
        /// Tests have no Application.Current — they stay synchronous.
        /// </summary>
        internal async Task OpenFromPathAsync(string path)
        {
            if (_state.IsBusy || string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            bool useLoader = Application.Current?.Dispatcher != null;
            ParsedSession parsed;
            try
            {
                if (useLoader)
                {
                    _state.IsBusy = true;
                    await PumpLoaderAsync();
                    parsed = await Task.Run(() => ParseSessionFile(path));
                }
                else
                    parsed = ParseSessionFile(path);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open session");
                if (useLoader)
                    _state.IsBusy = false;
                _dialogs.ShowError(string.Format(Locals.SessionInvalidFile + "\n{0}", ex.Message), Locals.Error);
                return;
            }

            if (parsed.Error != null)
            {
                if (useLoader)
                    _state.IsBusy = false;
                if (parsed.UnsupportedVersion != null)
                    _dialogs.ShowError(string.Format(Locals.SessionUnsupportedVersion, parsed.UnsupportedVersion), Locals.Error);
                else
                    _dialogs.ShowError(Locals.SessionInvalidFile, Locals.Error);
                return;
            }

            if (useLoader)
                _state.IsBusy = false;

            if (_session.EntryCount > 0
                && !_dialogs.Confirm(Locals.SessionReplaceConfirm, Locals.Warning))
                return;

            if (useLoader)
            {
                _state.IsBusy = true;
                await PumpLoaderAsync();
            }

            try
            {
                ApplyParsedSession(parsed);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply session");
                _dialogs.ShowError(string.Format(Locals.SessionInvalidFile + "\n{0}", ex.Message), Locals.Error);
            }
            finally
            {
                if (useLoader)
                    _state.IsBusy = false;
            }
        }

        private static Task PumpLoaderAsync()
        {
            return Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render).Task;
        }

        private static ParsedSession ParseSessionFile(string path)
        {
            try
            {
                var document = SavedSessionXmlStore.LoadFile(path);
                return new ParsedSession
                {
                    Document = document,
                    Entries = SavedSessionMapper.ToEntries(document)
                };
            }
            catch (SavedSessionUnsupportedVersionException ex)
            {
                return new ParsedSession { UnsupportedVersion = ex.Version, Error = ex };
            }
            catch (SavedSessionFormatException ex)
            {
                Logger.Warn(ex, "Invalid session file");
                return new ParsedSession { Error = ex };
            }
        }

        private void ApplyParsedSession(ParsedSession parsed)
        {
            var document = parsed.Document;
            var entries = parsed.Entries ?? new List<LogEntry>();

            _receivers.Pause();
            _clean();
            // Don't Receive would skip restored rows on AddEntries — apply it after the buffer is filled.
            _coordinator.ClearLoggerExclusions();

            var knownPaths = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                string loggerPath = entries[i]?.FullPath;
                if (!string.IsNullOrEmpty(loggerPath))
                    knownPaths.Add(loggerPath);
            }

            _coordinator.ApplySessionFilter(document, knownPaths, applyDontReceive: false);
            // Clean posted SessionCleared; that handler wipes Don't Show. Re-apply after the wipe.
            _tree.QueueDisplayFilterRestore();

            if (entries.Count > 0)
            {
                _bookmarks.QueueRestore(document.Bookmarks);
                _session.AddEntries(entries);
            }
            else
                _tree.TryApplyQueuedDisplayRestore();

            _coordinator.ApplySessionDontReceive(document);
            bool searchActive = _coordinator.IsSearchActive || _coordinator.IsTimeIntervalActive;
            _search.LoadFromPreset(
                document.SearchText ?? string.Empty,
                document.MatchCase,
                document.MatchWholeWord,
                document.UseRegex,
                document.MatchLogLevel,
                searchActive);
            _setMinLevelUi(_coordinator.CurrentMinLevel);
        }

        private SavedSessionDocument BuildDocument()
        {
            var document = new SavedSessionDocument { Version = SavedSessionDocument.CurrentVersion };
            _coordinator.CaptureSessionFilter(document, _tree.CollectIncludedRoots(), _tree.CollectUncheckedLoggerPaths());

            var entries = _session.GetAllEntries();
            document.Entries = new List<SavedSessionEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                document.Entries.Add(SavedSessionMapper.ToDto(entries[i]));

            document.Bookmarks = CaptureBookmarks(entries);
            return document;
        }

        private sealed class ParsedSession
        {
            public SavedSessionDocument Document;
            public List<LogEntry> Entries;
            public Exception Error;
            public string UnsupportedVersion;
        }

        private List<SavedSessionBookmark> CaptureBookmarks(IReadOnlyList<LogEntry> entries)
        {
            var result = new List<SavedSessionBookmark>();
            if (_bookmarks.Bookmarks.Count == 0 || entries == null || entries.Count == 0)
                return result;

            var used = new bool[entries.Count];
            foreach (var bookmark in _bookmarks.Bookmarks)
            {
                if (bookmark?.Log == null)
                    continue;
                int index = IndexOfEntry(entries, bookmark.Log, used);
                if (index < 0)
                    continue;
                used[index] = true;
                result.Add(new SavedSessionBookmark
                {
                    Index = index,
                    Comment = bookmark.Comment ?? string.Empty
                });
            }

            return result;
        }

        private static int IndexOfEntry(IReadOnlyList<LogEntry> entries, LogMessage log, bool[] used)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (used[i])
                    continue;
                var entry = entries[i];
                if (entry.Time == log.Time
                    && (int)entry.Level == (int)log.Level
                    && entry.Thread == log.Thread
                    && entry.ProcessID == log.ProcessID
                    && entry.Logger == log.Logger
                    && entry.Address == log.Address
                    && entry.Message == log.Message)
                    return i;
            }

            return -1;
        }
    }
}
