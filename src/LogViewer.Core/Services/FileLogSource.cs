using System;
using System.IO;
using System.Text;
using System.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.Services
{
    /// <remarks>
    /// Each Start creates a new run token. The poll thread sleeps up to a second, so after a quick
    /// Stop → Start the old thread wakes up while <c>_running</c> is true again; it compares its token with
    /// the current one and exits instead of tailing the file in parallel (which duplicated lines).
    /// </remarks>
    public class FileLogSource : ILogSource
    {
        private readonly string _filePath;
        private readonly TemplateParseLayout _layout;
        private readonly TemplateLogParser _parser;
        private readonly Encoding _encoding;
        private volatile bool _running;
        private volatile object _run;
        private Thread _watchThread;
        private long _position;

        public FileLogSource(string filePath, LogTemplateDto template, string encoding = "UTF-8")
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            if (template == null) throw new ArgumentNullException(nameof(template));
            _encoding = Encoding.GetEncoding(encoding ?? "UTF-8");
            _parser = new TemplateLogParser();
            _layout = TemplateParseLayout.Create(template);
        }

        public void Start()
        {
            if (_running)
                return;

            _position = 0;
            try
            {
                using (var stream = TemplateFileLogReader.OpenRead(_filePath))
                    _position = stream.Length;
            }
            catch { }

            var run = new object();
            _run = run;
            _running = true;
            _watchThread = new Thread(() => WatchLoop(run)) { IsBackground = true };
            _watchThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _run = null;
            _watchThread = null;
        }

        private bool IsCurrent(object run)
        {
            return _running && ReferenceEquals(run, _run);
        }

        private void WatchLoop(object run)
        {
            while (IsCurrent(run))
            {
                try
                {
                    long currentLength;
                    using (var stream = TemplateFileLogReader.OpenRead(_filePath))
                        currentLength = stream.Length;

                    if (currentLength > _position)
                    {
                        // The writer may append between measuring the length and reading; the read goes
                        // to the real end, so continue from where it stopped, not from the stale length.
                        long readTo = ReadNewContent(_position);
                        _position = readTo > _position ? readTo : currentLength;
                    }
                }
                catch (Exception)
                {
                    // log and continue
                }

                Thread.Sleep(1000);
            }
        }

        /// <returns>Stream position after the read, or -1 if the file could not be read.</returns>
        private long ReadNewContent(long fromPosition)
        {
            try
            {
                using (var stream = TemplateFileLogReader.OpenRead(_filePath))
                {
                    stream.Position = fromPosition;
                    TemplateFileLogReader.Read(
                        stream,
                        _encoding,
                        _parser,
                        _layout,
                        _filePath,
                        entry => LogReceived?.Invoke(this, new LogEntryReceivedEventArgs { Entry = entry }),
                        CancellationToken.None,
                        null);
                    return stream.Position;
                }
            }
            catch (Exception)
            {
                // raise error entry or ignore
                return -1;
            }
        }

        public event EventHandler<LogEntryReceivedEventArgs> LogReceived;
    }
}
