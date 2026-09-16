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
}
