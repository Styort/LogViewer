using System;

namespace LogViewer.Core.Domain
{
    public enum ImportRangeMode
    {
        EntireFile,
        LastBytes,
        LastDuration
    }

    public sealed class ImportRange
    {
        public const long DefaultLastBytes = 50L * 1024 * 1024;

        public ImportRangeMode Mode { get; set; }
        public long LastBytes { get; set; }
        public TimeSpan LastDuration { get; set; }

        public static ImportRange Entire { get; } = new ImportRange();

        public ImportRange()
        {
            Mode = ImportRangeMode.EntireFile;
            LastBytes = DefaultLastBytes;
            LastDuration = TimeSpan.FromHours(2);
        }
    }

    /// <summary>
    /// Progress of a single file inside a multi-file import. <see cref="Percent"/> is 0..100.
    /// The aggregate <c>IProgress&lt;int&gt;</c> cannot be mapped back to a file without rounding loss.
    /// </summary>
    public struct ImportFileProgress
    {
        public ImportFileProgress(int fileIndex, int percent)
        {
            FileIndex = fileIndex;
            Percent = percent;
        }

        public int FileIndex { get; }
        public int Percent { get; }
    }
}
