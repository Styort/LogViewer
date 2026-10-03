using System.Collections;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.MVVM.Models;

namespace LogViewer.Adapters
{
    /// <summary>
    /// Read-only view of UI rows as their Core entries, for Core services that take
    /// <see cref="IReadOnlyList{LogEntry}"/> (navigation, grouping).
    /// </summary>
    /// <remarks>
    /// Index i of the view is index i of the rows, so a result index selects the matching row directly.
    /// Nothing is copied: converting a million rows on every F3 used to allocate a million entries. The view
    /// reads the live list, so it must be used on the thread that owns the list, or over a snapshot.
    /// </remarks>
    public sealed class LogMessageEntryView : IReadOnlyList<LogEntry>
    {
        private readonly IReadOnlyList<LogMessage> _rows;

        public LogMessageEntryView(IReadOnlyList<LogMessage> rows)
        {
            _rows = rows ?? new LogMessage[0];
        }

        public int Count => _rows.Count;

        public LogEntry this[int index] => _rows[index]?.Entry;

        public IEnumerator<LogEntry> GetEnumerator()
        {
            for (int i = 0; i < _rows.Count; i++)
                yield return _rows[i]?.Entry;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
