using LogViewer.Core.Services;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// UI binding for a watched file backed by Core FileLogSource.
    /// </summary>
    public class WatchedFileInfo
    {
        public string FilePath { get; set; }
        public FileLogSource Source { get; set; }
    }
}
