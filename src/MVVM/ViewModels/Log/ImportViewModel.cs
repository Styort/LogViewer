using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LogViewer.Adapters;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.TreeView;
using LogViewer.Services;
using NLog;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// File/archive import and live follow. _importData avoids re-parsing the same path
    /// and lets the tree drop an imported slice without a full Clean.
    /// </summary>
    public sealed class ImportViewModel : BaseViewModel, IResettable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly LogViewState _state;
        private readonly LogSession _session;
        private readonly LogProcessingService _processing;
        private readonly ILogImportService _importService;
        private readonly CoreToUiAdapter _adapter;
        private readonly LogFileWatchService _watch;
        private readonly IDialogService _dialogs;
        private readonly IFileDialogService _files;
        private readonly ReceiversViewModel _receivers;
        private readonly Func<LogMessage, string> _formatLine;
        private readonly Dictionary<string, List<LogMessage>> _importData = new Dictionary<string, List<LogMessage>>();
        private CancellationTokenSource _cancelImport = new CancellationTokenSource();
        private bool _startReadFromFileIsEnabled = true;
        private bool _isVisibleProcessBar;
        private int _processBarValue;
        private RelayCommand _startFileCommand;
        private RelayCommand _pauseFileCommand;
        private RelayCommand _importCommand;
        private RelayCommand _exportCommand;

        public ImportViewModel(
            LogViewState state,
            LogSession session,
            LogProcessingService processing,
            ILogImportService importService,
            CoreToUiAdapter adapter,
            LogFileWatchService watch,
            IDialogService dialogs,
            IFileDialogService files,
            ReceiversViewModel receivers,
            Func<LogMessage, string> formatLine)
        {
            _state = state;
            _session = session;
            _processing = processing;
            _importService = importService;
            _adapter = adapter;
            _watch = watch;
            _dialogs = dialogs;
            _files = files;
            _receivers = receivers;
            _formatLine = formatLine;
        }

        /// <summary>For host Dispose: stop follow without going through the Import API.</summary>
        public LogFileWatchService Watch => _watch;

        /// <summary>The Start/Pause file toolbar looks at Count.</summary>
        public List<WatchedFileInfo> FileWatchers => _watch.FileWatchers;

        /// <summary>true = follow is paused. Same idea as StartIsEnabled for UDP.</summary>
        public bool StartReadFromFileIsEnabled
        {
            get => _startReadFromFileIsEnabled;
            set
            {
                _startReadFromFileIsEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Circular progress over the list. Clears IsBusy on state.</summary>
        public bool IsVisibleProcessBar
        {
            get => _isVisibleProcessBar;
            set
            {
                _isVisibleProcessBar = value;
                if (!_isVisibleProcessBar)
                    ProcessBarValue = 0;
                _state.IsBusy = _isVisibleProcessBar;
                OnPropertyChanged();
            }
        }

        /// <summary>0..100. Drives TaskBarFileLoadProgress for the taskbar icon.</summary>
        public int ProcessBarValue
        {
            get => _processBarValue;
            set
            {
                _processBarValue = value;
                TaskBarFileLoadProgress = _processBarValue == 0 ? 0 : (double)_processBarValue / 100;
                OnPropertyChanged(nameof(TaskBarFileLoadProgress));
                OnPropertyChanged();
            }
        }

        /// <summary>0..1 fraction for TaskbarItemInfo.ProgressValue.</summary>
        public double TaskBarFileLoadProgress { get; set; }

        public RelayCommand StartFileReadingCommand => _startFileCommand ?? (_startFileCommand = new RelayCommand(StartFileReading));
        public RelayCommand PauseFileReadingCommand => _pauseFileCommand ?? (_pauseFileCommand = new RelayCommand(StopFileReading));
        public RelayCommand ImportCommand => _importCommand ?? (_importCommand = new RelayCommand(ImportLogs));
        public RelayCommand ExportCommand => _exportCommand ?? (_exportCommand = new RelayCommand(ExportLogs));

        /// <summary>The tree drops an imported slice: whether this path was in the last import.</summary>
        public bool ContainsImportPath(string path) => _importData.ContainsKey(path);

        /// <summary>After Don't Receive / Ignore IP on a file logger.</summary>
        public void RemoveImportPath(string path)
        {
            if (_importData.ContainsKey(path))
                _importData.Remove(path);
        }

        /// <inheritdoc />
        public void Reset()
        {
            _importData.Clear();
            _watch.Reset();
        }

        private void StartFileReading()
        {
            StartReadFromFileIsEnabled = false;
            _watch.StartAll();
        }

        private void StopFileReading()
        {
            StartReadFromFileIsEnabled = true;
            _watch.StopAll();
            // A file batch may still sit in the adapter — otherwise extra rows appear after Pause.
            _adapter.FlushPending();
        }

        /// <summary>
        /// Drag&amp;drop, Import button, and command-line args. async void — otherwise Command cannot take Task.
        /// </summary>
        public async void ImportLogs(object obj)
        {
            List<ImportLogFile> importLogFiles = new List<ImportLogFile>();
            _cancelImport = new CancellationTokenSource();
            var extractDirectories = new List<string>();
            var extractedLogPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var selectedPaths = new List<string>();
                if (obj is IEnumerable<string> filesPath && filesPath.All(x => !string.IsNullOrEmpty(x) && File.Exists(x)))
                    selectedPaths.AddRange(filesPath);
                else if (obj is string logPath && !string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                    selectedPaths.Add(logPath);
                else
                {
                    var names = _files.OpenFiles("Logs and archives (*.txt;*.log;*.zip;*.rar)|*.txt;*.log;*.zip;*.rar|All files (*.*)|*.*");
                    if (names != null)
                        selectedPaths.AddRange(names);
                }

                if (!selectedPaths.Any())
                    return;

                var hadArchive = false;
                foreach (var path in selectedPaths)
                {
                    if (ArchiveLogExtractor.IsArchive(path))
                    {
                        hadArchive = true;
                        var extractDir = ArchiveLogExtractor.CreateExtractDirectory(path);
                        extractDirectories.Add(extractDir);
                        try
                        {
                            var extracted = ArchiveLogExtractor.ExtractLogFiles(path, extractDir);
                            foreach (var logFile in extracted.LogFiles)
                            {
                                extractedLogPaths.Add(logFile);
                                if (CheckFileExistsInImportLogs(logFile)) continue;
                                AddImportedLogFile(logFile, importLogFiles);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Failed to extract archive {0}", path);
                            _dialogs.ShowError(string.Format(Locals.ArchiveExtractFailed, path, ex.Message), Locals.Error);
                        }
                    }
                    else
                    {
                        if (CheckFileExistsInImportLogs(path)) continue;
                        AddImportedLogFile(path, importLogFiles);
                    }
                }

                if (!importLogFiles.Any())
                {
                    if (hadArchive)
                        _dialogs.ShowWarning(Locals.ArchiveHasNoLogFiles, Locals.Error);
                    return;
                }

                var templateResult = _dialogs.ShowImportTemplate(importLogFiles.First().FilePath);
                if (!templateResult.Confirmed)
                    return;

                var template = templateResult.Template;
                _receivers.Pause();

                List<WatchedFileInfo> currentFileWatchers = new List<WatchedFileInfo>();
                if (templateResult.NeedUpdateFile)
                {
                    var dtoForTail = LogTemplateAdapter.ToDto(template);
                    if (dtoForTail != null)
                    {
                        foreach (var importLog in importLogFiles)
                        {
                            if (extractedLogPaths.Contains(importLog.FilePath))
                                continue;

                            if (!_watch.ContainsPath(importLog.FilePath))
                            {
                                try
                                {
                                    var fileSource = new FileLogSource(importLog.FilePath, dtoForTail, template.Encoding ?? "UTF-8");
                                    _processing.AddSource(fileSource);
                                    fileSource.Start();
                                    currentFileWatchers.Add(new WatchedFileInfo { FilePath = importLog.FilePath, Source = fileSource });
                                }
                                catch { }
                            }
                        }
                    }
                }

                if (importLogFiles.Count > 1)
                    _dialogs.ShowImportProgress(importLogFiles, () => _cancelImport.Cancel());

                var paths = importLogFiles.Select(x => x.FilePath).ToList();
                var dto = LogTemplateAdapter.ToDto(template);
                if (dto == null) return;

                IsVisibleProcessBar = true;
                var progress = new Progress<int>(p => ProcessBarValue = p);

                try
                {
                    var importRange = templateResult.ImportRange ?? ImportRange.Entire;
                    int importedCount = await _importService.ImportFromFilesAsync(
                        paths, dto, progress, _cancelImport.Token, importRange);
                    if (importedCount == 0 && importRange.Mode != ImportRangeMode.EntireFile)
                        _dialogs.ShowWarning(Locals.ImportRangeNoEntries, Locals.Information);
                }
                catch (OperationCanceledException)
                {
                    foreach (var path in paths)
                        _session.RemoveEntriesBySource(path);
                    foreach (var path in paths)
                        RemoveImportPath(path);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "An error occurred while importing log files");
                    _dialogs.ShowError(string.Format("{0}\n{1}", Locals.IncorrectLogMessageTemplateMessageBoxInfo, ex.Message), Locals.Error);
                }
                finally
                {
                    IsVisibleProcessBar = false;
                    if (templateResult.NeedUpdateFile)
                    {
                        StartReadFromFileIsEnabled = false;
                        foreach (var w in currentFileWatchers)
                            _watch.Add(w);
                    }

                    foreach (var path in extractedLogPaths)
                        RemoveImportPath(path);
                }
            }
            finally
            {
                ArchiveLogExtractor.Cleanup(extractDirectories, Logger);
            }
        }

        private void ExportLogs(object obj)
        {
            Logger.Debug($"ExportLogs with {obj}");
            if (_state.IsBusy) return;

            _receivers.Pause();

            Node node = obj as Node;
            string filePrefix = node == null ? string.Empty : $"{node.Text}_";
            var fileName = _files.SaveFile(".txt", "Text document|*.txt", $"LogViewerExportLog_{filePrefix}{DateTime.Now:yy-MM-dd}");
            if (string.IsNullOrEmpty(fileName))
                return;

            try
            {
                _state.IsBusy = true;
                IEnumerable<LogMessage> logsToExport = _state.Logs;
                if (node != null && node.Logger != "Root")
                    logsToExport = _state.Logs.Where(x => x.FullPath.Contains(node.Logger));
                var txtLogs = logsToExport.Select(_formatLine).ToList();
                File.WriteAllLines(fileName, txtLogs, Encoding.UTF8);
                _files.OpenFolder(Path.GetDirectoryName(fileName));
            }
            finally
            {
                _state.IsBusy = false;
            }
        }

        private void AddImportedLogFile(string filePath, List<ImportLogFile> importLogFiles)
        {
            _importData.Add(filePath, new List<LogMessage>());
            importLogFiles.Add(new ImportLogFile
            {
                FilePath = filePath,
                Process = 0,
                FileName = Path.GetFileName(filePath)
            });
        }

        private bool CheckFileExistsInImportLogs(string filePath)
        {
            if (_importData.ContainsKey(filePath) || _watch.ContainsPath(filePath))
            {
                _dialogs.ShowInformation(string.Format(Locals.FileAlreadyAdded, filePath));
                return true;
            }
            return false;
        }
    }
}
