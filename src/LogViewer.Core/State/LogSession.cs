using System;
using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.State
{
    /// <summary>
    /// Holds all log entries and filter criteria. Thread-safe. No UI.
    /// </summary>
    public class LogSession
    {
        private readonly object _lock = new object();
        private readonly List<LogEntry> _allEntries = new List<LogEntry>();

        public FilterCriteria FilterCriteria { get; } = new FilterCriteria();

        public bool AllowMaxMessageBufferSize { get; set; }
        public int MaxMessageBufferSize { get; set; } = 1000000;
        public int DeletedMessagesCount { get; set; } = 100000;

        /// <summary>
        /// Raised when entries are added or removed (e.g. buffer trim). Raised on background thread.
        /// </summary>
        public event EventHandler<LogSessionChangedEventArgs> SessionChanged;

        /// <summary>
        /// Raised when filter criteria are updated. Raised on caller thread.
        /// </summary>
        public event EventHandler FilterCriteriaChanged;

        public int EntryCount
        {
            get { lock (_lock) return _allEntries.Count; }
        }

        public void AddEntry(LogEntry entry)
        {
            if (entry == null) return;

            int removedCount = 0;
            lock (_lock)
            {
                if (AllowMaxMessageBufferSize && _allEntries.Count >= MaxMessageBufferSize && DeletedMessagesCount > 0)
                {
                    int toRemove = Math.Min(DeletedMessagesCount, _allEntries.Count);
                    _allEntries.RemoveRange(0, toRemove);
                    removedCount = toRemove;
                }
                _allEntries.Add(entry);
            }

            SessionChanged?.Invoke(this, new LogSessionChangedEventArgs { AddedEntry = entry, RemovedCount = removedCount });
        }

        public void AddEntries(IEnumerable<LogEntry> entries)
        {
            if (entries == null) return;
            var list = entries.ToList();
            if (list.Count == 0) return;

            int removedCount = 0;
            lock (_lock)
            {
                while (AllowMaxMessageBufferSize && _allEntries.Count + list.Count > MaxMessageBufferSize && DeletedMessagesCount > 0)
                {
                    int toRemove = Math.Min(DeletedMessagesCount, _allEntries.Count);
                    if (toRemove <= 0) break;
                    _allEntries.RemoveRange(0, toRemove);
                    removedCount += toRemove;
                }
                _allEntries.AddRange(list);
            }

            SessionChanged?.Invoke(this, new LogSessionChangedEventArgs { AddedEntries = list, RemovedCount = removedCount });
        }

        public void Clear()
        {
            lock (_lock)
            {
                _allEntries.Clear();
            }
            SessionChanged?.Invoke(this, new LogSessionChangedEventArgs { Cleared = true });
        }

        public IReadOnlyList<LogEntry> GetAllEntries()
        {
            lock (_lock)
            {
                return _allEntries.ToList();
            }
        }

        public IReadOnlyList<LogEntry> GetFilteredEntries(ILogFilter filter)
        {
            if (filter == null) return new List<LogEntry>();
            lock (_lock)
            {
                return _allEntries.Where(e => filter.ShouldInclude(e, FilterCriteria)).ToList();
            }
        }

        public void RemoveEntriesBySource(string sourceAddress)
        {
            lock (_lock)
            {
                _allEntries.RemoveAll(e => e.Address == sourceAddress);
            }
            SessionChanged?.Invoke(this, new LogSessionChangedEventArgs { Cleared = false });
            FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveEntriesByLogger(string loggerFullPathContains)
        {
            lock (_lock)
            {
                _allEntries.RemoveAll(e => e.FullPath != null && e.FullPath.Contains(loggerFullPathContains));
            }
            SessionChanged?.Invoke(this, new LogSessionChangedEventArgs { Cleared = false });
            FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Updates filter criteria from the given instance and raises FilterCriteriaChanged.
        /// </summary>
        public void SetFilterCriteria(FilterCriteria criteria)
        {
            if (criteria == null) return;
            FilterCriteria.MinLevel = criteria.MinLevel;
            FilterCriteria.ExcludedLoggerFullPaths = new HashSet<string>(criteria.ExcludedLoggerFullPaths ?? new HashSet<string>());
            FilterCriteria.ExcludedLoggerFullPathsWithBuffer = new HashSet<string>(criteria.ExcludedLoggerFullPathsWithBuffer ?? new HashSet<string>());
            FilterCriteria.SearchText = criteria.SearchText ?? string.Empty;
            FilterCriteria.MatchCase = criteria.MatchCase;
            FilterCriteria.MatchWholeWord = criteria.MatchWholeWord;
            FilterCriteria.UseRegex = criteria.UseRegex;
            FilterCriteria.MatchLogLevel = criteria.MatchLogLevel;
            FilterCriteria.IsSearchActive = criteria.IsSearchActive;
            FilterCriteria.IsTimeIntervalActive = criteria.IsTimeIntervalActive;
            FilterCriteria.TimeRangeFrom = criteria.TimeRangeFrom;
            FilterCriteria.TimeRangeTo = criteria.TimeRangeTo;
            FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Builds logger hierarchy from current entries. One root per Address, then ExecutableName (if any), then logger path segments. Leaf nodes have FullPath set.
        /// </summary>
        public IReadOnlyList<LoggerTreeNode> GetLoggerHierarchy()
        {
            List<LoggerTreeNode> roots;
            lock (_lock)
            {
                var byAddress = _allEntries.GroupBy(e => e.Address ?? "").OrderBy(g => g.Key).ToList();
                roots = new List<LoggerTreeNode>();
                foreach (var grp in byAddress)
                {
                    string address = grp.Key;
                    var root = new LoggerTreeNode { Name = address, FullPath = "" };
                    var exeGroups = grp.GroupBy(e => e.ExecutableName ?? "").ToList();
                    foreach (var exeGrp in exeGroups)
                    {
                        string exe = exeGrp.Key;
                        if (!string.IsNullOrEmpty(exe))
                        {
                            var exeNode = new LoggerTreeNode { Name = exe, FullPath = "" };
                            AddLoggerPaths(exeNode, exeGrp.Select(e => e.Logger).Distinct().ToList(), address + "." + exe + ".");
                            root.Children.Add(exeNode);
                        }
                        else
                            AddLoggerPaths(root, exeGrp.Select(e => e.Logger).Distinct().ToList(), address + ".");
                    }
                    if (root.Children.Count > 0)
                        roots.Add(root);
                }
            }
            return roots;
        }

        private static void AddLoggerPaths(LoggerTreeNode parent, List<string> loggerPaths, string prefix)
        {
            if (loggerPaths == null || loggerPaths.Count == 0) return;
            var byFirst = loggerPaths
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => l.Split(new[] { '.' }, 2, StringSplitOptions.None))
                .GroupBy(parts => parts[0])
                .ToList();
            foreach (var grp in byFirst)
            {
                string seg = grp.Key;
                var rest = grp.Where(p => p.Length > 1).Select(p => p[1]).Distinct().ToList();
                bool isLeaf = grp.Any(p => p.Length == 1);
                string fullPath = prefix + seg;
                var node = new LoggerTreeNode { Name = seg, FullPath = isLeaf ? fullPath : "" };
                if (rest.Count > 0)
                    AddLoggerPaths(node, rest, fullPath + ".");
                parent.Children.Add(node);
            }
        }
    }

    public class LogSessionChangedEventArgs : EventArgs
    {
        public LogEntry AddedEntry { get; set; }
        public List<LogEntry> AddedEntries { get; set; }
        public int RemovedCount { get; set; }
        public bool Cleared { get; set; }
    }
}
