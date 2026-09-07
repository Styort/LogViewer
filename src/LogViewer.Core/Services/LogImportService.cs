using System;
using System.Collections.Generic;
using System.IO;
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

            var paths = filePaths.ToList();
            if (paths.Count == 0) return;

            var logTypeMarkers = BuildLogTypeMarkers(template);
            var encoding = Encoding.GetEncoding(template.Encoding ?? "UTF-8");
            int completed = 0;

            foreach (var filePath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entries = new List<LogEntry>();
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, encoding))
                {
                    var sb = new StringBuilder();
                    string line;
                    long lastReportedPos = 0;

                    while ((line = reader.ReadLine()) != null && !cancellationToken.IsCancellationRequested)
                    {
                        if (StringUtils.ContainsAnyOf(line, logTypeMarkers, true))
                        {
                            if (sb.Length > 0)
                            {
                                var entry = _parser.ParseLine(sb.ToString(), template, filePath);
                                if (entry != null)
                                    entries.Add(entry);
                            }
                            sb.Clear();
                        }
                        else if (sb.Length > 0)
                        {
                            sb.Append(Environment.NewLine);
                        }
                        sb.Append(line);

                        if (stream.Length > 0 && stream.Position - lastReportedPos > 65536)
                        {
                            lastReportedPos = stream.Position;
                            int pct = (int)((double)stream.Position / stream.Length * 100);
                            progress?.Report(Math.Min(100, (completed * 100 + pct) / paths.Count));
                        }
                    }

                    if (sb.Length > 0)
                    {
                        var entry = _parser.ParseLine(sb.ToString(), template, filePath);
                        if (entry != null)
                            entries.Add(entry);
                    }
                }

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

        private static string[] BuildLogTypeMarkers(LogTemplateDto template)
        {
            var levels = new[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };
            var list = new List<string>();
            if (!template.TemplateParameterses.ContainsKey(ImportTemplateParameters.level))
                return list.ToArray();

            int levelIdx = template.TemplateParameterses[ImportTemplateParameters.level];
            int maxIdx = template.TemplateParameterses.Values.Max();
            string sep = template.Separator ?? ";";

            foreach (var level in levels)
            {
                if (levelIdx == 0)
                    list.Add(sep + level);
                else if (levelIdx == maxIdx)
                    list.Add(level + sep);
                else
                    list.Add(sep + level + sep);
            }
            return list.ToArray();
        }
    }
}
