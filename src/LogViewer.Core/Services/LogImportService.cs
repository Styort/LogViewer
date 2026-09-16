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

        public void ImportFromFiles(
            IEnumerable<string> filePaths,
            LogTemplateDto template,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            if (filePaths == null || template == null) return;

            var paths = filePaths as IList<string> ?? filePaths.ToList();
            if (paths.Count == 0) return;

            var layout = TemplateParseLayout.Create(template);
            if (!layout.IsValid) return;

            var encoding = Encoding.GetEncoding(template.Encoding ?? "UTF-8");
            int completed = 0;

            foreach (var filePath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entries = new List<LogEntry>();
                using (var stream = TemplateFileLogReader.OpenRead(filePath))
                {
                    TemplateFileLogReader.Read(
                        stream,
                        encoding,
                        _parser,
                        layout,
                        filePath,
                        entries.Add,
                        cancellationToken,
                        (pos, len) =>
                        {
                            int pct = (int)((double)pos / len * 100);
                            progress?.Report(Math.Min(100, (completed * 100 + pct) / paths.Count));
                        });
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (entries.Count > 0)
                    _session.AddEntries(entries);

                completed++;
                progress?.Report((completed * 100) / paths.Count);
            }
        }

        public Task ImportFromFilesAsync(
            IEnumerable<string> filePaths,
            LogTemplateDto template,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => ImportFromFiles(filePaths, template, progress, cancellationToken), cancellationToken);
        }
    }
}
