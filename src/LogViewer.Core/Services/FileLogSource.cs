using System;
using System.IO;
using System.Text;
using System.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.Services
{
    public class FileLogSource : ILogSource
    {
        private readonly string _filePath;
        private readonly TemplateParseLayout _layout;
        private readonly TemplateLogParser _parser;
        private readonly Encoding _encoding;
        private volatile bool _running;
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
            _position = 0;
            try
            {
                using (var stream = TemplateFileLogReader.OpenRead(_filePath))
                    _position = stream.Length;
            }
            catch { }

            _running = true;
            _watchThread = new Thread(WatchLoop) { IsBackground = true };
            _watchThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _watchThread = null;
        }

        private void WatchLoop()
        {
            while (_running)
            {
                try
                {
                    long currentLength;
                    using (var stream = TemplateFileLogReader.OpenRead(_filePath))
                        currentLength = stream.Length;

                    if (currentLength > _position)
                    {
                        ReadNewContent(_position);
                        _position = currentLength;
                    }
                }
                catch (Exception)
                {
                    // log and continue
                }

                Thread.Sleep(1000);
            }
        }

        private void ReadNewContent(long fromPosition)
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
                }
            }
            catch (Exception)
            {
                // raise error entry or ignore
            }
        }

        public event EventHandler<LogEntryReceivedEventArgs> LogReceived;
    }
}
