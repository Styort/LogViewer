using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Services;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.Services
{
    /// <summary>
    /// Live file follow: FileLogSource lives here, not in ImportViewModel,
    /// because the tree (Ignore IP / Don't Receive) also removes watchers.
    /// </summary>
    public sealed class LogFileWatchService : BaseViewModel, IResettable
    {
        private readonly LogProcessingService _processing;

        public LogFileWatchService(LogProcessingService processing)
        {
            _processing = processing;
        }

        /// <summary>Active follow files. The same list is bound to the Start/Pause file toolbar.</summary>
        public List<WatchedFileInfo> FileWatchers { get; } = new List<WatchedFileInfo>();

        /// <summary>Add and notify the UI. Source is not Start yet — ImportViewModel does that.</summary>
        public void Add(WatchedFileInfo watched)
        {
            if (watched == null)
                return;
            FileWatchers.Add(watched);
            OnPropertyChanged(nameof(FileWatchers));
        }

        /// <summary>
        /// Find a watcher by path suffix (tree node text) or exact FilePath == nodeLogger.
        /// </summary>
        public WatchedFileInfo Find(string filePathOrNodeText, string nodeLogger)
        {
            return FileWatchers.FirstOrDefault(x =>
                x.FilePath.EndsWith(filePathOrNodeText) || nodeLogger != null && x.FilePath == nodeLogger);
        }

        /// <summary>Already watching this path — re-importing the same file must not create another FileLogSource.</summary>
        public bool ContainsPath(string filePath)
        {
            return FileWatchers.Any(x => x.FilePath == filePath);
        }

        /// <summary>Stop and remove from processing. Otherwise follow keeps writing after Don't Receive on a file.</summary>
        public void Remove(WatchedFileInfo watched)
        {
            if (watched == null)
                return;
            watched.Source?.Stop();
            _processing.RemoveSource(watched.Source);
            FileWatchers.Remove(watched);
            OnPropertyChanged(nameof(FileWatchers));
        }

        /// <summary>Start file reading button: all tails resume.</summary>
        public void StartAll()
        {
            foreach (var w in FileWatchers)
                w.Source?.Start();
        }

        /// <summary>Pause follow without removing sources (StartAll can resume).</summary>
        public void StopAll()
        {
            foreach (var w in FileWatchers)
                w.Source?.Stop();
        }

        /// <summary>Clean / Dispose: stop and drop all FileLogSource from processing.</summary>
        public void RemoveAll()
        {
            foreach (var w in FileWatchers)
            {
                w.Source?.Stop();
                _processing.RemoveSource(w.Source);
            }
            FileWatchers.Clear();
            OnPropertyChanged(nameof(FileWatchers));
        }

        /// <inheritdoc />
        public void Reset()
        {
            RemoveAll();
        }
    }
}
