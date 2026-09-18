using System;
using System.Collections.Generic;
using System.Threading;
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
    /// and flushed in batches (see <see cref="UiLogEntryBatcher"/>) so a UDP storm does not Post
    /// once per packet. SessionCleared / EntriesRemoved / FilteredViewUpdated stay one-shot on the UI
    /// thread, but pending batches are flushed or discarded first so add-then-trim order is preserved
    /// and Clear never paints entries Core already dropped.
    /// </remarks>
    public class CoreToUiAdapter : IDisposable
    {
        private readonly SynchronizationContext _uiContext;
        private readonly LogProcessingService _service;
        private readonly UiLogEntryBatcher _batcher;
        private bool _subscribed;

        public CoreToUiAdapter(SynchronizationContext uiContext, LogProcessingService service)
        {
            _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _batcher = new UiLogEntryBatcher(PostBatchToUi);
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
                _uiContext.Post(_ => RaiseFilteredViewUpdated(includeAllEntries: true), null);
            }
        }

        private void OnFilterCriteriaChanged(object sender, EventArgs e)
        {
            _batcher.Flush();
            _uiContext.Post(_ => RaiseFilteredViewUpdated(includeAllEntries: false), null);
        }

        private void RaiseFilteredViewUpdated(bool includeAllEntries = false)
        {
            IReadOnlyList<LogEntry> allEntries = null;
            IReadOnlyList<LogEntry> entries;
            if (includeAllEntries)
            {
                allEntries = _service.Session.GetAllEntries();
                var filter = _service.Filter;
                var criteria = _service.Session.FilterCriteria;
                var filtered = new List<LogEntry>(allEntries.Count);
                for (int i = 0; i < allEntries.Count; i++)
                {
                    var item = allEntries[i];
                    if (filter.ShouldInclude(item, criteria))
                        filtered.Add(item);
                }
                entries = filtered;
            }
            else
            {
                entries = _service.GetFilteredEntries();
            }
            FilteredViewUpdated?.Invoke(this, new FilteredViewUpdatedEventArgs { Entries = entries, AllEntries = allEntries });
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
    }
}
