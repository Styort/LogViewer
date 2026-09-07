using LogViewer.Core.Domain;

namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// Determines whether an entry should be included in the filtered view.
    /// </summary>
    public interface ILogFilter
    {
        bool ShouldInclude(LogEntry entry, FilterCriteria criteria);
    }
}
