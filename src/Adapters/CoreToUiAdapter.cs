using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Subscribes to LogProcessingService and LogSession, marshals events to the UI thread.
    /// </summary>
    /// <remarks>
    /// EntryProcessed from Core still arrives on the receive thread. Those notifications are queued
    /// and flushed in batches (see <see cref="LogEntryBatcher"/>) so a UDP storm does not Post
    /// once per packet. SessionCleared / EntriesRemoved / FilteredViewUpdated stay one-shot on the UI
    /// thread, but pending batches are flushed or discarded first so add-then-trim order is preserved
    /// and Clear never paints entries Core already dropped.
    /// <para>
    /// Refiltering (criteria change, bulk import) runs on the thread pool: the session lock is held only
    /// to copy the buffer, and the O(N) filter pass neither freezes the window nor blocks receive threads.
    /// Each request gets a generation number; a newer request cancels the older pass and a stale result
    /// is dropped on the UI thread. <see cref="FilteredViewUpdatedEventArgs.SnapshotSequence"/> tells the
    /// presenter which entries the snapshot already covered, because batches keep arriving meanwhile.
    /// </para>
    /// </remarks>
    public class CoreToUiAdapter : IDisposable
    {
        private readonly SynchronizationContext _uiContext;
        private readonly LogProcessingService _service;
        private readonly LogEntryBatcher _batcher;
        private bool _subscribed;

        // UI thread only.
        private int _refilterGeneration;
        private CancellationTokenSource _refilterCancellation;
        private bool _refilterNeedsAllEntries;

        public CoreToUiAdapter(SynchronizationContext uiContext, LogProcessingService service)
        {
            _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _batcher = new LogEntryBatcher(PostBatchToUi);
        }

        /// <summary>
        /// Raised on the UI thread with one or more entries in receive order.
        /// </summary>
        public event EventHandler<LogEntriesProcessedEventArgs> EntriesProcessed;

        /// <summary>
        /// Raised on UI thread when session was cleared. UI should clear its collections.
        /// </summary>
        public event EventHandler SessionCleared;

        /// <summary>
        /// Raised on UI thread when buffer trim removed entries. UI should remove first count from its collections.
        /// </summary>
        public event Action<int> EntriesRemoved;

        /// <summary>
        /// Raised on UI thread when the filtered view should be replaced (e.g. criteria change or bulk import).
        /// </summary>
        public event EventHandler<FilteredViewUpdatedEventArgs> FilteredViewUpdated;

        public void Subscribe()
        {
            if (_subscribed) return;
            _service.EntryProcessed += OnEntryProcessed;
            _service.Session.SessionChanged += OnSessionChanged;
            _service.Session.FilterCriteriaChanged += OnFilterCriteriaChanged;
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed) return;
            _service.EntryProcessed -= OnEntryProcessed;
            _service.Session.SessionChanged -= OnSessionChanged;
            _service.Session.FilterCriteriaChanged -= OnFilterCriteriaChanged;
            _subscribed = false;
            // Last packets after Stop must not sit in the timer until the next Start/Clear.
            _batcher.Flush();
        }

        /// <summary>
        /// Pushes any queued entries to the UI immediately (Pause/Stop/Dispose).
        /// </summary>
        public void FlushPending()
        {
            _batcher.Flush();
        }

        public void Dispose()
        {
            Unsubscribe();
            _refilterCancellation?.Cancel();
            _batcher.Dispose();
        }

        private void PostBatchToUi(IReadOnlyList<LogEntryProcessedEventArgs> batch, int epoch)
        {
            _uiContext.Post(_ =>
            {
                if (!_batcher.IsCurrentEpoch(epoch))
                    return;
                EntriesProcessed?.Invoke(this, new LogEntriesProcessedEventArgs { Entries = batch });
            }, null);
        }

        private void OnEntryProcessed(object sender, LogEntryProcessedEventArgs e)
        {
            _batcher.Enqueue(e);
        }

        private void OnSessionChanged(object sender, LogSessionChangedEventArgs e)
        {
            if (e.Cleared)
            {
                _batcher.Discard();
                _uiContext.Post(_ => SessionCleared?.Invoke(sender, EventArgs.Empty), null);
                return;
            }

            if (e.RemovedCount > 0)
            {
                _batcher.Flush();
                var removed = e.RemovedCount;
                _uiContext.Post(_ => EntriesRemoved?.Invoke(removed), null);
                return;
            }

            if (e.AddedEntries != null && e.AddedEntries.Count > 0)
            {
                _batcher.Flush();
                _uiContext.Post(_ => StartRefilter(includeAllEntries: true), null);
            }
        }

        private void OnFilterCriteriaChanged(object sender, EventArgs e)
        {
            _batcher.Flush();
            _uiContext.Post(_ => StartRefilter(includeAllEntries: false), null);
        }

        /// <summary>UI thread. Supersedes any refilter still running.</summary>
        private void StartRefilter(bool includeAllEntries)
        {
            // A criteria change must not swallow a pending bulk-import rebuild of AllLogs.
            _refilterNeedsAllEntries |= includeAllEntries;
            bool withAllEntries = _refilterNeedsAllEntries;

            _refilterCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _refilterCancellation = cancellation;
            int generation = ++_refilterGeneration;

            var session = _service.Session;
            var filter = _service.Filter;
            var token = cancellation.Token;
            Task.Run(() =>
            {
                var all = session.GetSnapshot(out long lastSequence);
                var filtered = LogSession.Filter(all, filter, session.FilterCriteria, token);
                return new FilteredViewUpdatedEventArgs
                {
                    Entries = filtered,
                    AllEntries = withAllEntries ? all : null,
                    SnapshotSequence = lastSequence
                };
            }, token).ContinueWith(task =>
            {
                if (task.IsCanceled)
                    return;
                _uiContext.Post(_ => CompleteRefilter(task, generation, cancellation), null);
            }, TaskScheduler.Default);
        }

        /// <summary>UI thread.</summary>
        private void CompleteRefilter(Task<FilteredViewUpdatedEventArgs> task, int generation, CancellationTokenSource cancellation)
        {
            cancellation.Dispose();
            if (generation != _refilterGeneration)
                return;
            _refilterCancellation = null;

            if (task.IsFaulted)
            {
                // Rethrow on the UI thread so the global handler logs and reports it; a silent failure
                // would leave the list showing the previous filter.
                ExceptionDispatchInfo.Capture(task.Exception.GetBaseException()).Throw();
            }

            var args = task.Result;
            if (args.AllEntries != null)
                _refilterNeedsAllEntries = false;
            FilteredViewUpdated?.Invoke(this, args);
        }
    }

    public class LogEntriesProcessedEventArgs : EventArgs
    {
        public IReadOnlyList<LogEntryProcessedEventArgs> Entries { get; set; }
    }

    public class FilteredViewUpdatedEventArgs : EventArgs
    {
        public IReadOnlyList<LogEntry> Entries { get; set; }
        /// <summary>
        /// When set (e.g. bulk import), UI should also update its full list from this.
        /// </summary>
        public IReadOnlyList<LogEntry> AllEntries { get; set; }

        /// <summary>
        /// Last <see cref="LogEntry.Sequence"/> stored when the snapshot was taken. Entries up to this value
        /// were already considered by the snapshot; batches that arrive later may still contain some of them.
        /// </summary>
        public long SnapshotSequence { get; set; }
    }
}
