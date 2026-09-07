using LogViewer.Core.Domain;

namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// Parses raw input into a LogEntry.
    /// </summary>
    public interface ILogParser
    {
        LogEntry Parse(string raw);
    }
}
