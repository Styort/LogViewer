using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.Core.State;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Imports log entries from files using TemplateLogParser. No UI.
    /// </summary>
    public class LogImportService : ILogImportService
    {
        private readonly LogSession _session;
        private readonly TemplateLogParser _parser = new TemplateLogParser();

        public LogImportService(LogSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public int ImportFromFiles(
            IEnumerable<string> filePaths,
            LogTemplateDto template,
            IProgress<int> progress,
            CancellationToken cancellationToken,
            ImportRange range = null,
            IProgress<ImportFileProgress> fileProgress = null)
        {
            if (filePaths == null || template == null) return 0;

            var paths = filePaths as IList<string> ?? filePaths.ToList();
            if (paths.Count == 0) return 0;

            var layout = TemplateParseLayout.Create(template);
            if (!layout.IsValid) return 0;

            var encoding = Encoding.GetEncoding(template.Encoding ?? "UTF-8");
            int completed = 0;
            int totalEntries = 0;

            foreach (var filePath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entries = new List<LogEntry>();
                using (var stream = TemplateFileLogReader.OpenRead(filePath))
                {
                    var seek = TemplateFileLogReader.SeekToRange(
                        stream, range, encoding, _parser, layout, cancellationToken);

                    Action<LogEntry> onEntry = entries.Add;
                    if (seek.FilterByMinTime)
                    {
                        var cutoff = seek.MinTime;
                        onEntry = e =>
                        {
                            if (e.Time >= cutoff)
                                entries.Add(e);
                        };
                    }

                    TemplateFileLogReader.Read(
                        stream,
                        encoding,
                        _parser,
                        layout,
                        filePath,
                        onEntry,
                        cancellationToken,
                        (pos, len) =>
                        {
                            int pct = len <= 0 ? 100 : (int)((double)pos / len * 100);
                            if (pct > 100)
                                pct = 100;
                            progress?.Report(Math.Min(100, (completed * 100 + pct) / paths.Count));
                            fileProgress?.Report(new ImportFileProgress(completed, pct));
                        });
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (entries.Count > 0)
                {
                    // Import respects Don't Receive the same way live UDP does (before session storage).
                    entries.RemoveAll(e => !_session.ShouldStoreInBuffer(e));
                    if (entries.Count > 0)
                    {
                        _session.AddEntries(entries);
                        totalEntries += entries.Count;
                    }
                }

                completed++;
                progress?.Report((completed * 100) / paths.Count);
                fileProgress?.Report(new ImportFileProgress(completed - 1, 100));
            }

            return totalEntries;
        }

        public Task<int> ImportFromFilesAsync(
            IEnumerable<string> filePaths,
            LogTemplateDto template,
            IProgress<int> progress,
            CancellationToken cancellationToken,
            ImportRange range = null,
            IProgress<ImportFileProgress> fileProgress = null)
        {
            return Task.Run(
                () => ImportFromFiles(filePaths, template, progress, cancellationToken, range, fileProgress),
                cancellationToken);
        }
    }
}
