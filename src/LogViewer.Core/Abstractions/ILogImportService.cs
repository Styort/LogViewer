using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// Imports log entries from files into the session. No UI. Progress and cancellation for UI binding.
    /// </summary>
    public interface ILogImportService
    {
        /// <summary>
        /// Reads files, parses with template, adds entries to session. Reports progress 0-100.
        /// Cancel leaves partial state (entries from already-processed files remain).
        /// </summary>
        void ImportFromFiles(
            IEnumerable<string> filePaths,
            LogTemplateDto template,
            IProgress<int> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// Async wrapper for ImportFromFiles so UI can await without blocking.
        /// </summary>
        Task ImportFromFilesAsync(
            IEnumerable<string> filePaths,
            LogTemplateDto template,
            IProgress<int> progress,
            CancellationToken cancellationToken);
    }
}
