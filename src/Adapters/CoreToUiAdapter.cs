using System;
using System.Collections.Generic;
using System.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Subscribes to LogProcessingService and LogSession, marshals events to UI thread.
    /// </summary>
    public class CoreToUiAdapter
    {
        private readonly SynchronizationContext _uiContext;
        private readonly LogProcessingService _service;
        private bool _subscribed;

        public CoreToUiAdapter(SynchronizationContext uiContext, LogProcessingService service)
        {
            _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Raised on UI thread when a log entry was processed. UI should convert to LogMessage and add to collections.
        /// </summary>
        public event EventHandler<LogEntryProcessedEventArgs> EntryProcessed;

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
        }

        private void OnEntryProcessed(object sender, LogEntryProcessedEventArgs e)
        {
            _uiContext.Post(_ =>
            {
                EntryProcessed?.Invoke(sender, e);
            }, null);
        }

        private void OnSessionChanged(object sender, LogSessionChangedEventArgs e)
        {
            _uiContext.Post(_ =>
            {
                if (e.Cleared)
                    SessionCleared?.Invoke(sender, EventArgs.Empty);
                else if (e.RemovedCount > 0)
                    EntriesRemoved?.Invoke(e.RemovedCount);
                else if (e.AddedEntries != null && e.AddedEntries.Count > 0)
                    RaiseFilteredViewUpdated(includeAllEntries: true);
            }, null);
        }

        private void OnFilterCriteriaChanged(object sender, EventArgs e)
        {
            _uiContext.Post(_ => RaiseFilteredViewUpdated(includeAllEntries: false), null);
        }

        private void RaiseFilteredViewUpdated(bool includeAllEntries = false)
        {
            var entries = _service.GetFilteredEntries();
            var allEntries = includeAllEntries ? _service.Session.GetAllEntries() : null;
            FilteredViewUpdated?.Invoke(this, new FilteredViewUpdatedEventArgs { Entries = entries, AllEntries = allEntries });
        }
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
