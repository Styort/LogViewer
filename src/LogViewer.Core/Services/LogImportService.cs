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
            // One session add at the end. Each AddEntries posts a full list rebuild, and those
            // Normal-priority posts starve Render, so the import dialog only paints a few frames.
            var pending = new List<LogEntry>();
            int lastProgressTick = 0;

            try
            {
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
                                ReportProgress(completed, pct, false);
                            });
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (entries.Count > 0)
                    {
                        // Import respects Don't Receive the same way live UDP does (before session storage).
                        entries.RemoveAll(e => !_session.ShouldStoreInBuffer(e));
                        if (entries.Count > 0)
                        {
                            pending.AddRange(entries);
                            totalEntries += entries.Count;
                        }
                    }

                    completed++;
                    ReportProgress(completed - 1, 100, false);
                }
            }
            finally
            {
                // Cancel drops the batch; the caller also removes by path. Any other exit keeps files already read.
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (completed > 0)
                        ReportProgress(completed - 1, 100, true);
                    if (pending.Count > 0)
                        _session.AddEntries(pending);
                }
            }

            return totalEntries;

            void ReportProgress(int fileIndex, int pct, bool force)
            {
                // Progress<T> Posts at DispatcherPriority.Normal, above Render. Unthrottled reports
                // (every 64 KB, every file) keep the dispatcher from painting the progress window.
                int now = Environment.TickCount;
                if (!force && lastProgressTick != 0 && unchecked(now - lastProgressTick) < ProgressIntervalMs)
                    return;
                lastProgressTick = now == 0 ? 1 : now;
                progress?.Report(Math.Min(100, (fileIndex * 100 + pct) / paths.Count));
                fileProgress?.Report(new ImportFileProgress(fileIndex, pct));
            }
        }

        private const int ProgressIntervalMs = 80;

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
