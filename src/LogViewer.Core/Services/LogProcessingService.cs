using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;
using LogViewer.Core.State;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Orchestrates log sources, session, and filter. No UI. Raises events for UI to marshal and display.
    /// </summary>
    public class LogProcessingService
    {
        private readonly LogSession _session;
        private readonly ILogFilter _filter;
        private readonly List<ILogSource> _sources = new List<ILogSource>();

        public LogProcessingService(LogSession session, ILogFilter filter)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        public LogSession Session => _session;
        public ILogFilter Filter => _filter;

        /// <summary>
        /// Raised when a log entry is received and added to session. Raised on background thread.
        /// IncludedInFilter indicates whether the entry passes the current filter for display.
        /// </summary>
        public event EventHandler<LogEntryProcessedEventArgs> EntryProcessed;

        public void AddSource(ILogSource source)
        {
            if (source == null) return;
            source.LogReceived += OnSourceLogReceived;
            _sources.Add(source);
        }

        public void RemoveSource(ILogSource source)
        {
            if (source == null) return;
            source.LogReceived -= OnSourceLogReceived;
            _sources.Remove(source);
            source.Stop();
        }

        public void RemoveAllSources()
        {
            foreach (var source in _sources)
            {
                source.LogReceived -= OnSourceLogReceived;
                source.Stop();
            }
            _sources.Clear();
        }

        public void StartAllSources()
        {
            foreach (var source in _sources)
                source.Start();
        }

        public void StopAllSources()
        {
            foreach (var source in _sources)
                source.Stop();
        }

        public void ClearSession()
        {
            _session.Clear();
        }

        /// <summary>
        /// Updates filter criteria; session will raise FilterCriteriaChanged.
        /// </summary>
        public void SetFilterCriteria(FilterCriteria criteria)
        {
            _session?.SetFilterCriteria(criteria);
        }

        /// <summary>
        /// Returns current filtered entries using session's filter criteria.
        /// </summary>
        public IReadOnlyList<LogEntry> GetFilteredEntries()
        {
            return _session?.GetFilteredEntries(_filter) ?? new List<LogEntry>();
        }

        private void OnSourceLogReceived(object sender, LogEntryReceivedEventArgs e)
        {
            if (e?.Entry == null) return;
            _session.AddEntry(e.Entry);
            bool includedInFilter = _filter.ShouldInclude(e.Entry, _session.FilterCriteria);
            EntryProcessed?.Invoke(this, new LogEntryProcessedEventArgs
            {
                Entry = e.Entry,
                IncludedInFilter = includedInFilter
            });
        }
    }

    public class LogEntryProcessedEventArgs : EventArgs
    {
        public LogEntry Entry { get; set; }
        public bool IncludedInFilter { get; set; }
    }
}
