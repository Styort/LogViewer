using LogViewer.Core.Domain;

namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// Determines whether an entry should be included in the filtered view (display only).
    /// Buffer exclusion (Don't Receive) is <c>FilterCriteria.ShouldStoreInBuffer</c>, not this method.
    /// </summary>
    public interface ILogFilter
    {
        bool ShouldInclude(LogEntry entry, FilterCriteria criteria);
    }
}
