using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;

namespace LogViewer.Core.Services
{
    public class FileLogSource : ILogSource
    {
        private readonly string _filePath;
        private readonly LogTemplateDto _template;
        private readonly TemplateLogParser _parser;
        private readonly string[] _logTypeMarkers;
        private readonly string _encoding;
        private volatile bool _running;
        private Thread _watchThread;
        private long _position;

        public FileLogSource(string filePath, LogTemplateDto template, string encoding = "UTF-8")
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _template = template ?? throw new ArgumentNullException(nameof(template));
            _encoding = encoding ?? "UTF-8";
            _parser = new TemplateLogParser();
            _logTypeMarkers = BuildLogTypeMarkers(template);
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

        public void Start()
        {
            _position = 0;
            try
            {
                using (var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                    using (var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        currentLength = stream.Length;

                    if (currentLength > _position)
                    {
                        ReadNewContent(_position, currentLength);
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

        private void ReadNewContent(long fromPosition, long toLength)
        {
            try
            {
                using (var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Position = fromPosition;
                    var enc = Encoding.GetEncoding(_encoding);
                    using (var reader = new StreamReader(stream, enc))
                    {
                        var sb = new StringBuilder();
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (StringUtils.ContainsAnyOf(line, _logTypeMarkers, true))
                            {
                                if (sb.Length > 0)
                                {
                                    var entry = _parser.ParseLine(sb.ToString(), _template, _filePath);
                                    if (entry != null)
                                        LogReceived?.Invoke(this, new LogEntryReceivedEventArgs { Entry = entry });
                                }
                                sb.Clear();
                            }
                            else if (sb.Length > 0)
                            {
                                sb.Append(Environment.NewLine);
                            }
                            sb.Append(line);
                        }
                        if (sb.Length > 0)
                        {
                            var entry = _parser.ParseLine(sb.ToString(), _template, _filePath);
                            if (entry != null)
                                LogReceived?.Invoke(this, new LogEntryReceivedEventArgs { Entry = entry });
                        }
                    }
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
