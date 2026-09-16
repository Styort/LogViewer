using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using LogViewer.Adapters;
using LogViewer.Factories;
using LogViewer.Core.Domain;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.TreeView;
using LogViewer.MVVM.Views;
using NLog;
using Application = System.Windows.Application;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using LogLevel = LogViewer.Core.Domain.LogLevel;

namespace LogViewer.MVVM.ViewModels
{
    public class LogViewModel : BaseViewModel, IDisposable
    {
        #region Поля

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const int RECEIVER_COLUMN_WIDTH = 15;
        private const string TRANSPARENT_COLOR = "#00FFFFFF";

        private bool filterChanged = false;
        private bool _treeCheckJustDone;
        private readonly List<UdpLogSource> udpSources = new List<UdpLogSource>();
        private LogSession session;
        private ILogFilter coreFilter;
        private LogProcessingService processingService;
        private CoreToUiAdapter coreToUiAdapter;
        private ILogImportService logImportService;
        private readonly UdpSourceFactory udpSourceFactory = new UdpSourceFactory();

        // весь список классов, который имеется за текущий сеанс
        private HashSet<string> availableLoggers = new HashSet<string>();

        // список классов, которые не должны отображаться
        private HashSet<string> exceptLoggers = new HashSet<string>();

        // список классов, которые не должны отображаться и не должны добавлятся в буфер
        private HashSet<string> exceptLoggersWithBuffer = new HashSet<string>();

        private readonly List<Receiver> receivers;

        // коллекция всех логов
        private AsyncObservableCollection<LogMessage> allLogs = new AsyncObservableCollection<LogMessage>();

        private AsyncObservableCollection<LogMessage> logs = new AsyncObservableCollection<LogMessage>();
        private CancellationTokenSource cancellationToken;
        private bool startIsEnabled = true;
        private bool startReadFromFileIsEnabled = true;
        private bool cleanIsEnabled = false;
        private bool clearSearchResultIsEnabled = false;
        private bool isVisibleLoader = false;
        private bool isVisibleProcessBar = false;
        private bool isEnableLogList = true;
        private bool isMatchCase = false;
        private bool isMatchWholeWord = false;
        private bool isMatchLogLevel = true;
        private bool useRegularExpressions = false;
        private LogMessage selectedLog;
        private List<LogMessage> selectedLogs = new List<LogMessage>();
        private eLogLevel selectedMinLogLevel = eLogLevel.Trace;
        private Node selectedNode;
        private int receiverColorColumnWidth = 0;
        private bool allowMaxMessageBufferSize = false;
        private bool isEnableFindPrevious;
        private int maxMessageBufferSize = 0;
        private int deletedMessagesCount = 0;
        private int processBarValue = 0;
        private string searchText = string.Empty;
        private string highlightSearchText = string.Empty;
        private string loggerHighlightText = string.Empty;
        private SolidColorBrush iconColor = (SolidColorBrush)new BrushConverter().ConvertFrom("#3F51B5");
        private SolidColorBrush fontColor = new SolidColorBrush(Colors.White);
        private bool isSourceVisible = false;
        private bool isThreadVisible = true;
        private bool isEnableClearSearchLoggers;
        private string searchLoggerText = string.Empty;
        private DateTime goToTimestampDateTime;
        private DateTime fromTimeInverval;
        private DateTime toTimeInverval;
        private bool isSearchProcess = false;
        private bool isShowTaskbarProgress = false;
        private List<LogMessage> nearbyLastLogMessages = new List<LogMessage>();
        private LogMessage lastLogMessage;
        private LogMessage LastLogMessage
        {
            get => lastLogMessage;
            set
            {
                lastLogMessage = value;
                nearbyLastLogMessages.Clear();
                var index = Logs.IndexOf(lastLogMessage);
                if (index != -1)
                {
                    if (index != 0) nearbyLastLogMessages.Add(Logs[index - 1]);
                    if (Logs.Count > index + 1) nearbyLastLogMessages.Add(Logs[index + 1]);
                    var nearDebug = FindNearMessageByLogLevel(index, eLogLevel.Debug);
                    if (nearDebug != null) nearbyLastLogMessages.Add(nearDebug);
                    var nearWarn = FindNearMessageByLogLevel(index, eLogLevel.Warn);
                    if (nearWarn != null) nearbyLastLogMessages.Add(nearWarn);
                    var nearError = FindNearMessageByLogLevel(index, eLogLevel.Error);
                    if (nearError != null) nearbyLastLogMessages.Add(nearError);
                    var nearFatal = FindNearMessageByLogLevel(index, eLogLevel.Fatal);
                    if (nearFatal != null) nearbyLastLogMessages.Add(nearFatal);
                }
            }
        }

        private readonly Dictionary<string, eLogLevel> LogLevelMapping = new Dictionary<string, eLogLevel>
        {
            { "Trace", eLogLevel.Trace },
            { "Debug", eLogLevel.Debug },
            { "Info", eLogLevel.Info },
            { "Warn", eLogLevel.Warn },
            { "Error", eLogLevel.Error },
            { "Fatal", eLogLevel.Fatal },
        };

        #endregion

        #region Свойства

        public List<WatchedFileInfo> FileWatchers { get; set; } = new List<WatchedFileInfo>();

        public bool IsSearchProcess
        {
            get => isSearchProcess;
            set
            {
                isSearchProcess = value;
                ClearSearchResultIsEnabled = isSearchProcess || SearchText.Length > 0;
            }
        }

        /// <summary>
        /// Отвечает за показ изображения на кнопки запуска/паузы считывания логов по UDP
        /// </summary>
        public bool StartIsEnabled
        {
            get => startIsEnabled;
            set
            {
                startIsEnabled = value;
                IsShowTaskbarProgress = !startIsEnabled || !startReadFromFileIsEnabled;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Отвечает за показ изображения на кнопки запуска/паузы считывания логов из файла
        /// </summary>
        public bool StartReadFromFileIsEnabled
        {
            get => startReadFromFileIsEnabled;
            set
            {
                startReadFromFileIsEnabled = value;
                if (!startReadFromFileIsEnabled || !StartIsEnabled)
                    IsShowTaskbarProgress = true;
                else
                    IsShowTaskbarProgress = false;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Показывать или нет прогрессбар на иконке в таскбаре
        /// </summary>
        public bool IsShowTaskbarProgress
        {
            get => isShowTaskbarProgress;
            set
            {
                if (value && Settings.Instance.IsShowTaskbarProgress)
                    isShowTaskbarProgress = true;
                else
                    isShowTaskbarProgress = false;

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Активность кнопки очистки списка логов
        /// </summary>
        public bool CleanIsEnabled
        {
            get => cleanIsEnabled;
            set
            {
                cleanIsEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Активность кнопки очистки результата поиска
        /// </summary>
        public bool ClearSearchResultIsEnabled
        {
            get => clearSearchResultIsEnabled;
            set
            {
                clearSearchResultIsEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Отображаемые в списке логи
        /// </summary>
        public AsyncObservableCollection<LogMessage> Logs
        {
            get => logs;
            set
            {
                logs = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Коллекция всех классов - отображает дерево
        /// </summary>
        public AsyncObservableCollection<Node> Loggers { get; set; } = new AsyncObservableCollection<Node>();

        /// <summary>
        /// Выбранный лог
        /// </summary>
        public LogMessage SelectedLog
        {
            get => selectedLog;
            set
            {
                selectedLog = value;
                IsEnableFindPrevious = !string.IsNullOrEmpty(searchText) && SelectedLog != null;
                UpdateSelectedNode();
                OnPropertyChanged();
            }
        }

        public List<LogMessage> SelectedLogs
        {
            get => selectedLogs;
            set
            {
                selectedLogs = value ?? new List<LogMessage>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Выбранный элемент дерева (необходимо для выделения элемента дерева при нажатии на лог)
        /// </summary>
        public Node SelectedNode
        {
            get => selectedNode;
            set
            {
                if (selectedNode != null)
                    UpdateNodeSelections(selectedNode, false);
                selectedNode = value;
                if (selectedNode != null)
                    UpdateNodeSelections(selectedNode, true);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Список уровней логов (выпадающий список в столбце Level)
        /// </summary>
        public IEnumerable<eLogLevel> LogLevels => Enum.GetValues(typeof(eLogLevel)).Cast<eLogLevel>().OrderByDescending(x => x);

        /// <summary>
        /// Выбранный минимальный уровень лога
        /// </summary>
        public eLogLevel SelectedMinLogLevel
        {
            get => selectedMinLogLevel;
            set
            {
                selectedMinLogLevel = value;
                SyncFilterCriteriaToSession();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Видимость лоадера
        /// </summary>
        public bool IsVisibleLoader
        {
            get => isVisibleLoader;
            set
            {
                isVisibleLoader = value;
                IsEnableLogList = !isVisibleLoader;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Видимость прогресс-бара
        /// </summary>
        public bool IsVisibleProcessBar
        {
            get => isVisibleProcessBar;
            set
            {
                isVisibleProcessBar = value;
                if (!isVisibleProcessBar) ProcessBarValue = 0;
                IsVisibleLoader = isVisibleProcessBar;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Активность списка логов
        /// </summary>
        public bool IsEnableLogList
        {
            get => isEnableLogList;
            set
            {
                isEnableLogList = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Учитывать ли регистр при поиске
        /// </summary>
        public bool IsMatchCase
        {
            get => isMatchCase;
            set
            {
                isMatchCase = value;
                prevFindNext = String.Empty;
                filterChanged = true;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Учитывать только слово целиком
        /// </summary>
        public bool IsMatchWholeWord
        {
            get => isMatchWholeWord;
            set
            {
                isMatchWholeWord = value;
                prevFindNext = String.Empty;
                filterChanged = true;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Учитывать ли уровень лога при поиске
        /// </summary>
        public bool IsMatchLogLevel
        {
            get => isMatchLogLevel;
            set
            {
                isMatchLogLevel = value;
                filterChanged = true;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Использовать регулярные выражения
        /// </summary>
        public bool UseRegularExpressions
        {
            get => useRegularExpressions;
            set
            {
                useRegularExpressions = value;
                filterChanged = true;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Ширина столбца с цветами ресивера
        /// </summary>
        public int ColorReceiverColumnWidth
        {
            get => receiverColorColumnWidth;
            set
            {
                receiverColorColumnWidth = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Текст поиска
        /// </summary>
        public string SearchText
        {
            get => searchText;
            set
            {
                searchText = value;
                ClearSearchResultIsEnabled = IsSearchProcess || SearchText.Length > 0;
                IsEnableFindPrevious = !string.IsNullOrEmpty(searchText) && SelectedLog != null;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Подсвечиваемый текст при поиске сообщений
        /// </summary>
        public string HighlightSearchText
        {
            get => highlightSearchText;
            set
            {
                highlightSearchText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Подсвечиваемый текст при поиске логгеров
        /// </summary>
        public string LoggerHighlightText
        {
            get => loggerHighlightText;
            set
            {
                loggerHighlightText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Видимость колонки с IP
        /// </summary>
        public bool IsSourceVisible
        {
            get => isSourceVisible;
            set
            {
                isSourceVisible = value;
                OnPropertyChanged(nameof(SourceColumnWidth));
                OnPropertyChanged();
            }
        }

        public bool IsThreadVisible
        {
            get => isThreadVisible;
            set
            {
                isThreadVisible = value;
                OnPropertyChanged(nameof(ThreadColumnWidth));
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Ширина колонки с IP
        /// </summary>
        public double SourceColumnWidth => IsSourceVisible ? 115 : 0;

        /// <summary>
        /// Ширина колонки Thread
        /// </summary>
        public double ThreadColumnWidth => IsThreadVisible ? Double.NaN : 0;

        public SolidColorBrush IconColor
        {
            get => iconColor;
            set
            {
                iconColor = value;
                OnPropertyChanged();
            }
        }

        public SolidColorBrush FontColor
        {
            get => fontColor;
            set
            {
                fontColor = value;
                OnPropertyChanged();
            }
        }

        public int ProcessBarValue
        {
            get => processBarValue;
            set
            {
                processBarValue = value;
                TaskBarFileLoadProgress = processBarValue == 0 ? 0 : (double)processBarValue / 100;
                OnPropertyChanged(nameof(TaskBarFileLoadProgress));
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Отображение процесса загрузки файла в таскбаре
        /// </summary>
        public double TaskBarFileLoadProgress { get; set; }

        /// <summary>
        /// Активность кнопки поиска предыдущего сообщения
        /// </summary>
        public bool IsEnableFindPrevious
        {
            get => isEnableFindPrevious;
            set
            {
                isEnableFindPrevious = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Активность кнопки очистки поиска логгеров
        /// </summary>
        public bool IsEnableClearSearchLoggers
        {
            get => isEnableClearSearchLoggers;
            set
            {
                isEnableClearSearchLoggers = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Текст поиска логгера
        /// </summary>
        public string SearchLoggerText
        {
            get => searchLoggerText;
            set
            {
                searchLoggerText = value;
                IsEnableClearSearchLoggers = isSearchLoggersProcess || searchLoggerText.Any();
                OnPropertyChanged();
            }
        }

        #endregion

        #region Конструктор

        public LogViewModel()
        {
            Logs = new AsyncObservableCollection<LogMessage>();
            cancellationToken = new CancellationTokenSource();

            IconColor = Settings.Instance.CurrentTheme.Color;
            FontColor = FontColor.FromARGB(Settings.Instance.FontColor);
            allowMaxMessageBufferSize = Settings.Instance.IsEnabledMaxMessageBufferSize;
            maxMessageBufferSize = Settings.Instance.MaxMessageBufferSize;
            deletedMessagesCount = Settings.Instance.DeletedMessagesCount;
            IsSourceVisible = Settings.Instance.IsShowSourceColumn;
            IsThreadVisible = Settings.Instance.IsShowThreadColumn;

            receivers = Settings.Instance.Receivers;

            Loggers.Add(new Node
            {
                Logger = "Root",
                Text = "Root",
                IsExpanded = true,
                IsChecked = true,
                Source = "-"
            });

            session = new LogSession();
            session.AllowMaxMessageBufferSize = allowMaxMessageBufferSize;
            session.MaxMessageBufferSize = maxMessageBufferSize;
            session.DeletedMessagesCount = deletedMessagesCount;
            coreFilter = new LogFilter();
            processingService = new LogProcessingService(session, coreFilter);
            coreToUiAdapter = new CoreToUiAdapter(SynchronizationContext.Current, processingService);
            coreToUiAdapter.EntryProcessed += OnCoreEntryProcessed;
            coreToUiAdapter.SessionCleared += OnCoreSessionCleared;
            coreToUiAdapter.EntriesRemoved += OnCoreEntriesRemoved;
            coreToUiAdapter.FilteredViewUpdated += OnFilteredViewUpdated;
            coreToUiAdapter.Subscribe();

            logImportService = new LogImportService(session);

            CreateUdpSourcesFromFactory();
            SyncFilterCriteriaToSession();

            ColorReceiverColumnWidth = receivers.Count == 1 || receivers.Where(r => r.IsActive).All(x => x.Color.Color == Colors.White) ? 0 : RECEIVER_COLUMN_WIDTH;

            if (Settings.Instance.AutoStartInStartup && !App.IsManualStartup)
                Start();
        }

        private void OnCoreEntryProcessed(object sender, LogEntryProcessedEventArgs e)
        {
            if (e?.Entry == null) return;
            var msg = LogEntryConverter.ToLogMessage(e.Entry, receivers);
            if (msg == null) return;
            var currentReceiver = receivers.FirstOrDefault(x => x.Port == e.Entry.ReceiverPort);
            if (currentReceiver != null)
            {
                msg.Receiver.Color = currentReceiver.Color;
                msg.Receiver.Name = currentReceiver.Name;
                if (Settings.Instance.ShowMessageHighlightByReceiverColor)
                {
                    var messageColor = msg.Receiver.Color.Clone();
                    messageColor.Opacity = 0.1;
                    msg.ToggleMark = messageColor;
                }
            }
            allLogs.Add(msg);
            if (e.IncludedInFilter)
                Logs.Add(msg);
            BuildTreeByMessage(msg, true);
            CleanIsEnabled = allLogs.Any();
        }

        private void OnCoreSessionCleared(object sender, EventArgs e)
        {
            allLogs.Clear();
            Logs.Clear();
            availableLoggers.Clear();
            exceptLoggers.Clear();
            exceptLoggersWithBuffer.Clear();
            var root = Loggers[0];
            root.Children.Clear();
            CleanIsEnabled = false;
        }

        private void OnCoreEntriesRemoved(int count)
        {
            if (count <= 0) return;
            LastLogMessage = SelectedLog;
            var allList = allLogs.ToList();
            var logsList = Logs.ToList();
            int removeAll = Math.Min(count, allList.Count);
            if (removeAll > 0) allList.RemoveRange(0, removeAll);
            int removeLogs = Math.Min(count, logsList.Count);
            if (removeLogs > 0) logsList.RemoveRange(0, removeLogs);
            allLogs = new AsyncObservableCollection<LogMessage>(allList);
            Logs = new AsyncObservableCollection<LogMessage>(logsList);
            SelectedLog = GetLastSelecterOrNearbyMessage();
        }

        private void OnFilteredViewUpdated(object sender, FilteredViewUpdatedEventArgs e)
        {
            if (e?.Entries == null) return;

            List<LogMessage> filtered;
            if (e.AllEntries != null)
            {
                var all = new List<LogMessage>(e.AllEntries.Count);
                var map = new Dictionary<LogEntry, LogMessage>(e.AllEntries.Count);
                for (int i = 0; i < e.AllEntries.Count; i++)
                {
                    var entry = e.AllEntries[i];
                    var msg = ToUiMessage(entry);
                    if (msg == null) continue;
                    all.Add(msg);
                    map[entry] = msg;
                }
                filtered = new List<LogMessage>(e.Entries.Count);
                for (int i = 0; i < e.Entries.Count; i++)
                {
                    var entry = e.Entries[i];
                    if (map.TryGetValue(entry, out var msg))
                        filtered.Add(msg);
                    else
                    {
                        msg = ToUiMessage(entry);
                        if (msg != null) filtered.Add(msg);
                    }
                }
                allLogs = new AsyncObservableCollection<LogMessage>(all);
                BuildLoggersFromCore();
            }
            else
            {
                filtered = new List<LogMessage>(e.Entries.Count);
                for (int i = 0; i < e.Entries.Count; i++)
                {
                    var msg = ToUiMessage(e.Entries[i]);
                    if (msg != null) filtered.Add(msg);
                }
            }

            Logs = new AsyncObservableCollection<LogMessage>(filtered);
            CleanIsEnabled = allLogs.Any();
            if (_treeCheckJustDone)
            {
                _treeCheckJustDone = false;
                SelectedLog = GetLastSelecterOrNearbyMessage();
            }
        }

        private LogMessage ToUiMessage(LogEntry entry)
        {
            var msg = LogEntryConverter.ToLogMessage(entry, receivers);
            if (msg == null) return null;
            var rec = receivers.FirstOrDefault(x => x.Port == entry.ReceiverPort);
            if (rec != null)
            {
                msg.Receiver.Color = rec.Color;
                msg.Receiver.Name = rec.Name;
                if (Settings.Instance.ShowMessageHighlightByReceiverColor)
                {
                    var mc = rec.Color.Clone();
                    mc.Opacity = 0.1;
                    msg.ToggleMark = mc;
                }
            }
            return msg;
        }

        /// <summary>
        /// Rebuilds Loggers tree from Core GetLoggerHierarchy and syncs checkbox state from FilterCriteria.
        /// </summary>
        private void BuildLoggersFromCore()
        {
            var roots = session.GetLoggerHierarchy();
            var excluded = session.FilterCriteria?.ExcludedLoggerFullPaths ?? new HashSet<string>();
            Loggers[0].Children.Clear();
            foreach (var r in roots)
            {
                var node = BuildNodeFromCore(r, Loggers[0], r.Name);
                if (node != null)
                {
                    node.IsRoot = true;
                    node.IsExpanded = true;
                    node.Source = r.Name;
                    node.Logger = r.Name;
                    node.IsChecked = !excluded.Contains(r.Name);
                    Loggers[0].Children.Add(node);
                }
            }
        }

        private Node BuildNodeFromCore(LoggerTreeNode core, Node parent, string address)
        {
            var node = new Node(parent, core.Name);
            if (parent == Loggers[0])
            {
                node.Source = core.Name;
                node.Logger = core.Name;
            }
            else if (!string.IsNullOrEmpty(core.FullPath))
                node.Logger = core.FullPath;
            var excluded = session.FilterCriteria?.ExcludedLoggerFullPaths;
            node.IsChecked = string.IsNullOrEmpty(core.FullPath)
                ? parent?.IsChecked
                : (excluded == null || !excluded.Contains(core.FullPath));
            node.IsVisible = true;
            foreach (var child in core.Children ?? new List<LoggerTreeNode>())
                node.Children.Add(BuildNodeFromCore(child, node, address));
            return node;
        }

        #endregion

        #region Команды

        private RelayCommand startCommand;
        private RelayCommand pauseCommand;
        private RelayCommand pauseFileReadingCommand;
        private RelayCommand startFileReadingCommand;
        private RelayCommand cleanCommand;
        private RelayCommand searchLogCommand;
        private RelayCommand findNextCommand;
        private RelayCommand findPreviousCommand;
        private RelayCommand treeViewElementCheckCommand;
        private RelayCommand openSettingsCommand;
        private RelayCommand copyMessageCommand;
        private RelayCommand copyLogCommand;
        private RelayCommand clearLoggersCommand;
        private RelayCommand collapsLoggersCommand;
        private RelayCommand expandChildrenCommand;
        private RelayCommand collapseChildrenCommand;
        private RelayCommand clearChildrenCommand;
        private RelayCommand showOnlyThisLoggerCommand;
        private RelayCommand dontReceiveThisLoggerCommand;
        private RelayCommand ignoreThisIPCommand;
        private RelayCommand dontShowThisLoggerCommand;
        private RelayCommand findNextWarningCommand;
        private RelayCommand findNextErrorCommand;
        private RelayCommand importCommand;
        private RelayCommand exportCommand;
        private RelayCommand сlearSearchResultCommand;
        private RelayCommand searchLoggersCommand;
        private RelayCommand clearSearchLoggerResultCommand;
        private RelayCommand goToTimestampCommand;
        private RelayCommand setTimeIntervalCommand;
        private RelayCommand toggleMarkCommand;
        private RelayCommand findInTreeCommand;

        public RelayCommand StartCommand => startCommand ?? (startCommand = new RelayCommand(Start));
        public RelayCommand PauseCommand => pauseCommand ?? (pauseCommand = new RelayCommand(Pause));
        public RelayCommand StartFileReadingCommand => startFileReadingCommand ?? (startFileReadingCommand = new RelayCommand(StartFileReading));
        public RelayCommand PauseFileReadingCommand => pauseFileReadingCommand ?? (pauseFileReadingCommand = new RelayCommand(StopFileReading));
        public RelayCommand CleanCommand => cleanCommand ?? (cleanCommand = new RelayCommand(Clean));
        public RelayCommand SearchLogCommand => searchLogCommand ?? (searchLogCommand = new RelayCommand(Search));
        public RelayCommand FindNextCommand => findNextCommand ?? (findNextCommand = new RelayCommand(FindNext));
        public RelayCommand FindPreviousCommand => findPreviousCommand ?? (findPreviousCommand = new RelayCommand(FindPrevious));
        public RelayCommand CopyMessageCommand => copyMessageCommand ?? (copyMessageCommand = new RelayCommand(CopyMessage));
        public RelayCommand CopyLogCommand => copyLogCommand ?? (copyLogCommand = new RelayCommand(CopyLogs));
        public RelayCommand ClearLoggersCommand => clearLoggersCommand ?? (clearLoggersCommand = new RelayCommand(ClearLoggers));
        public RelayCommand CollapseLoggersCommand => collapsLoggersCommand ?? (collapsLoggersCommand = new RelayCommand(CollapseAllLoggers));
        public RelayCommand TreeViewElementCheckCommand => treeViewElementCheckCommand ?? (treeViewElementCheckCommand = new RelayCommand(TreeViewElementCheck));
        public RelayCommand ExpandChildrenCommand => expandChildrenCommand ?? (expandChildrenCommand = new RelayCommand(ExpandTreeViewChildren));
        public RelayCommand CollapseChildrenCommand => collapseChildrenCommand ?? (collapseChildrenCommand = new RelayCommand(CollapseTreeViewChildren));
        public RelayCommand ClearChildrenCommand => clearChildrenCommand ?? (clearChildrenCommand = new RelayCommand(ClearChildrenLoggers));
        public RelayCommand OpenSettingsCommand => openSettingsCommand ?? (openSettingsCommand = new RelayCommand(OpenSettings));
        public RelayCommand ShowOnlyThisLoggerCommand => showOnlyThisLoggerCommand ?? (showOnlyThisLoggerCommand = new RelayCommand(ShowOnlyThisLogger));
        public RelayCommand DontReceiveThisLoggerCommand => dontReceiveThisLoggerCommand ?? (dontReceiveThisLoggerCommand = new RelayCommand(DontReceiveThisLogger));
        public RelayCommand IgnoreThisIPCommand => ignoreThisIPCommand ?? (ignoreThisIPCommand = new RelayCommand(IgnoreThisIP));
        public RelayCommand DontShowThisLoggerCommand => dontShowThisLoggerCommand ?? (dontShowThisLoggerCommand = new RelayCommand(DontShowThisLogger));
        public RelayCommand FindNextWarningCommand => findNextWarningCommand ?? (findNextWarningCommand = new RelayCommand(FindNextWarning));
        public RelayCommand FindNextErrorCommand => findNextErrorCommand ?? (findNextErrorCommand = new RelayCommand(FindNextError));
        public RelayCommand ImportCommand => importCommand ?? (importCommand = new RelayCommand(ImportLogs));
        public RelayCommand ExportCommand => exportCommand ?? (exportCommand = new RelayCommand(ExportLogs));
        public RelayCommand ClearSearchResultCommand => сlearSearchResultCommand ?? (сlearSearchResultCommand = new RelayCommand(ClearSearchResult));
        public RelayCommand SearchLoggersCommand => searchLoggersCommand ?? (searchLoggersCommand = new RelayCommand(SearchLoggers));
        public RelayCommand ClearSearchLoggerResultCommand => clearSearchLoggerResultCommand ?? (clearSearchLoggerResultCommand = new RelayCommand(ClearSearchLoggersResult));
        public RelayCommand GoToTimestampCommand => goToTimestampCommand ?? (goToTimestampCommand = new RelayCommand(GoToTimestamp));
        public RelayCommand SetTimeIntervalCommand => setTimeIntervalCommand ?? (setTimeIntervalCommand = new RelayCommand(SetTimeInterval));
        public RelayCommand ToggleMarkCommand => toggleMarkCommand ?? (toggleMarkCommand = new RelayCommand(ToggleMark));
        public RelayCommand FindInTreeCommand => findInTreeCommand ?? (findInTreeCommand = new RelayCommand(FindLoggerInTreeByMessage));

        #endregion

        #region Обработчики команд

        /// <summary>
        /// Запуск считывания логов
        /// </summary>
        private void Start()
        {
            if (!udpSources.Any())
            {
                MessageBox.Show(Locals.NoReceiversMessageBoxInfo, Locals.Information,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SyncFilterCriteriaToSession();
            processingService.StartAllSources();
            StartIsEnabled = false;
        }

        /// <summary>
        /// Остановка считывания логов
        /// </summary>
        private void Pause()
        {
            processingService.StopAllSources();
            StartIsEnabled = true;
        }

        /// <summary>
        /// Запустить считывание логов из файла (Core FileLogSource).
        /// </summary>
        private void StartFileReading()
        {
            StartReadFromFileIsEnabled = false;
            foreach (var w in FileWatchers)
                w.Source?.Start();
        }

        /// <summary>
        /// Остановить считывание логов из файла.
        /// </summary>
        private void StopFileReading()
        {
            StartReadFromFileIsEnabled = true;
            foreach (var w in FileWatchers)
                w.Source?.Stop();
        }

        /// <summary>
        /// Очистка списка логов
        /// </summary>
        private void Clean()
        {
            if (IsVisibleLoader) return;
            processingService.ClearSession();
            if (Logs.Any()) Logs.Clear();

            nextMessages.Clear();
            previousMessages.Clear();
            currentWarnLoggers.Clear();
            currentErrorLoggers.Clear();
            importData.Clear();
            currentExceptLoggers.Clear();
            exceptLoggersWithBuffer.Clear();

            allLogs.Clear();

            lastSelectedMessageCounter = 0;
            lastSelectedPreviousMessageCounter = 0;
            warnSearchCounter = 0;

            SearchText = string.Empty;
            HighlightSearchText = string.Empty;
            prevFindNext = string.Empty;
            prevFindPrevious = string.Empty;

            findNextPrevSelectedLog = null;
            findPrevousPrevSelectedLog = null;
            currentSearchLogger = null;
            prevSelectedWarnLog = null;
            prevSelectedErrorLog = null;

            RemoveAllFileWatchers();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            CleanIsEnabled = false;
        }

        private string currentSearch = string.Empty;

        /// <summary>
        /// Поиск логов по введенному значению
        /// </summary>
        private void Search(object obj)
        {
            logger.Debug($"Search with {SearchText}");
            if (IsVisibleLoader) return;

            if (string.IsNullOrEmpty(SearchText))
            {
                ClearSearchResult();
                return;
            }

            LastLogMessage = SelectedLog;
            currentSearch = SearchText;
            bool isOpenInAnotherWinow = (bool)obj;

            if (isOpenInAnotherWinow)
            {
                // Не включаем IsSearchProcess в основном окне — фильтруем отдельно с IsSearchActive=true
                var searchCriteria = CreateFilterCriteria(isSearchActive: true);
                var entries = session.GetAllEntries()
                    .Where(e => processingService.Filter.ShouldInclude(e, searchCriteria))
                    .ToList();
                var logMessages = entries.Select(e => LogEntryConverter.ToLogMessage(e, receivers)).Where(m => m != null).ToList();
                foreach (var msg in logMessages)
                {
                    var rec = receivers.FirstOrDefault(x => x.Port == msg.Receiver?.Port);
                    if (rec != null) { msg.Receiver.Color = rec.Color; msg.Receiver.Name = rec.Name; }
                }
                if (logMessages.Any())
                {
                    var sr = new SearchResult(logMessages, SearchText, IsMatchCase);
                    sr.Show();
                    sr.ShowLogEvent += (sender, message) => SelectedLog = message;
                }
                else
                    MessageBox.Show(Locals.NothingFoundMessageBoxInfo, Locals.Search, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                IsSearchProcess = true;
                SyncFilterCriteriaToSession();
                HighlightSearchText = SearchText;
            }
        }

        private string prevFindNext = string.Empty;
        private List<LogMessage> nextMessages = new List<LogMessage>();
        private int lastSelectedMessageCounter;
        private LogMessage findNextPrevSelectedLog = new LogMessage();

        /// <summary>
        /// Найти следующее сообщение, относительного текущего выбранного, которое содержит в себе значение из строки поиска
        /// </summary>
        private void FindNext()
        {
            if (IsVisibleLoader) return;
            try
            {
                if (string.IsNullOrEmpty(SearchText) && SelectedLog != null)
                {
                    SearchText = selectedLog.Message;
                    HighlightSearchText = SearchText;
                }
                if (string.IsNullOrEmpty(SearchText)) return;

                HighlightSearchText = SearchText;
                if (prevFindNext != SearchText || filterChanged)
                {
                    filterChanged = false;
                    lastSelectedMessageCounter = 0;
                    prevFindNext = SearchText;
                    nextMessages = new List<LogMessage>();
                }

                if (lastSelectedMessageCounter >= nextMessages.Count || findNextPrevSelectedLog != SelectedLog)
                {
                    if (SelectedLog != null)
                    {
                        lastSelectedMessageCounter = 0;
                        var selectedLogIndex = Logs.IndexOf(SelectedLog);
                        nextMessages = selectedLogIndex >= 0 && selectedLogIndex < Logs.Count - 1
                            ? Logs.Skip(selectedLogIndex + 1).ToList()
                            : new List<LogMessage>();
                    }
                    if (!nextMessages.Any())
                        nextMessages = Logs.Filter(SearchText, IsMatchCase, IsMatchWholeWord, UseRegularExpressions,
                            IsMatchLogLevel ? SelectedMinLogLevel : eLogLevel.Trace).ToList();
                    else
                        nextMessages = nextMessages.Filter(SearchText, IsMatchCase, IsMatchWholeWord, UseRegularExpressions,
                            IsMatchLogLevel ? SelectedMinLogLevel : eLogLevel.Trace).ToList();

                    if (!nextMessages.Any() || nextMessages.Count <= lastSelectedMessageCounter)
                        return;
                }

                SelectedLog = nextMessages[lastSelectedMessageCounter];
                findNextPrevSelectedLog = SelectedLog;
                lastSelectedMessageCounter++;
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while FindNext");
            }
        }

        private string prevFindPrevious = string.Empty;
        private List<LogMessage> previousMessages = new List<LogMessage>();
        private int lastSelectedPreviousMessageCounter;
        private LogMessage findPrevousPrevSelectedLog = new LogMessage();

        /// <summary>
        /// Найти предыдущее сообщение, относительного текущего выбранного, которое содержит в себе значение из строки поиска
        /// </summary>
        private void FindPrevious()
        {
            if (IsVisibleLoader) return;
            try
            {
                if (string.IsNullOrEmpty(SearchText) && SelectedLog != null)
                {
                    SearchText = selectedLog.Message;
                    HighlightSearchText = SearchText;
                }
                if (string.IsNullOrEmpty(SearchText)) return;

                HighlightSearchText = SearchText;
                if (prevFindPrevious != SearchText || filterChanged)
                {
                    filterChanged = false;
                    prevFindPrevious = SearchText;
                    previousMessages = new List<LogMessage>();
                    lastSelectedPreviousMessageCounter = -1;
                }

                if (lastSelectedPreviousMessageCounter == -1 || findPrevousPrevSelectedLog != SelectedLog)
                {
                    var selectedLogIndex = Logs.IndexOf(SelectedLog);
                    previousMessages = selectedLogIndex > 0 ? Logs.Take(selectedLogIndex).ToList() : new List<LogMessage>();

                    if (previousMessages.Any())
                        previousMessages = previousMessages.Filter(SearchText, IsMatchCase, IsMatchWholeWord, UseRegularExpressions,
                            IsMatchLogLevel ? SelectedMinLogLevel : eLogLevel.Trace).ToList();
                    else
                        return;

                    lastSelectedPreviousMessageCounter = previousMessages.Count - 1;
                    if (!previousMessages.Any() || lastSelectedPreviousMessageCounter < 0)
                        return;
                }

                SelectedLog = previousMessages[lastSelectedPreviousMessageCounter];
                findPrevousPrevSelectedLog = SelectedLog;
                lastSelectedPreviousMessageCounter--;
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while FindPrevious");
            }
        }

        /// <summary>
        /// Нажатие на чекбокс в списке классов (дереве). Обновляет исключённые логгеры в Core и запрашивает обновление списка через адаптер.
        /// </summary>
        private void TreeViewElementCheck(object obj)
        {
            IsVisibleLoader = true;
            CheckBox currentCheckBox = (CheckBox)obj;
            var node = (Node)currentCheckBox.DataContext;
            currentExceptLoggers.Clear();
            LastLogMessage = SelectedLog;

            if (currentCheckBox.IsChecked.HasValue && currentCheckBox.IsChecked.Value)
            {
                if (node.Parent == null)
                {
                    exceptLoggers.Clear();
                    exceptLoggersWithBuffer.Clear();
                }
                else
                {
                    UpdateAllChildInExceptLoggers(node, true);
                    exceptLoggers.Remove(node.Logger);
                    exceptLoggersWithBuffer.Remove(node.Logger);
                }
            }
            else
            {
                UpdateAllChildInExceptLoggers(node);
                currentExceptLoggers.Add(node.Logger);
                exceptLoggers.Add(node.Logger);
            }

            SyncFilterCriteriaToSession();
            _treeCheckJustDone = true;
            IsVisibleLoader = false;
        }

        /// <summary>
        /// Открыть окно настроек
        /// </summary>
        private void OpenSettings()
        {
            var isProgress = !StartIsEnabled;
            Pause();

            var settingsDialog = new Views.SettingsWindow();
            if (settingsDialog.ShowDialog() == true)
            {
                try
                {
                    IsVisibleLoader = true;
                    allowMaxMessageBufferSize = Settings.Instance.IsEnabledMaxMessageBufferSize;
                    maxMessageBufferSize = Settings.Instance.MaxMessageBufferSize;
                    deletedMessagesCount = Settings.Instance.DeletedMessagesCount;
                    IsSourceVisible = Settings.Instance.IsShowSourceColumn;
                    IsThreadVisible = Settings.Instance.IsShowThreadColumn;
                    session.AllowMaxMessageBufferSize = allowMaxMessageBufferSize;
                    session.MaxMessageBufferSize = maxMessageBufferSize;
                    session.DeletedMessagesCount = deletedMessagesCount;
                    FontColor = FontColor.FromARGB(Settings.Instance.FontColor);
                    if (Settings.Instance.CurrentTheme != null && !Equals(Settings.Instance.CurrentTheme.Color, IconColor))
                        IconColor = Settings.Instance.CurrentTheme.Color;

                    foreach (var receiver in Settings.Instance.Receivers)
                    {
                        var foundReceiver = receivers.FirstOrDefault(x => x.Port == receiver.Port);
                        if (foundReceiver == null)
                            receivers.Add(receiver);
                        else
                        {
                            if (foundReceiver.Color.Color != receiver.Color.Color)
                            {
                                foundReceiver.Color = receiver.Color;
                                foreach (var logMessage in allLogs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                    logMessage.Receiver.Color = foundReceiver.Color;
                                foreach (var logMessage in Logs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                    logMessage.Receiver.Color = foundReceiver.Color;
                            }
                            if (foundReceiver.Name != receiver.Name)
                            {
                                foundReceiver.Name = receiver.Name;
                                foreach (var log in allLogs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                    log.Receiver.Name = foundReceiver.Name;
                                foreach (var log in Logs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                    log.Receiver.Name = foundReceiver.Name;
                            }
                        }
                    }

                    foreach (var receiver in receivers.ToList())
                    {
                        if (Settings.Instance.Receivers.All(x => x.Port != receiver.Port))
                            receivers.Remove(receiver);
                    }

                    CreateUdpSourcesFromFactory();
                    ColorReceiverColumnWidth = receivers.Count(res => res.IsActive) == 1 || receivers.Where(r => r.IsActive).All(x => x.ColorString == Colors.White.ToString())
                        ? 0 : RECEIVER_COLUMN_WIDTH;
                }
                catch (Exception e)
                {
                    logger.Warn(e, "An error occurred while save settings");
                }
                finally
                {
                    IsVisibleLoader = false;
                    if (isProgress) Start();
                }
            }
            else if (isProgress) Start();
        }

        /// <summary>
        /// Копирует сообщение лога в буфер
        /// </summary>
        private void CopyMessage()
        {
            if (SelectedLog == null) return;
            Clipboard.SetDataObject(SelectedLog.Message);
        }

        /// <summary>
        /// Копирует выбранные логи (дата и все поля) в буфер обмена.
        /// </summary>
        private void CopyLogs()
        {
            var logsToCopy = GetLogsToCopy();
            if (logsToCopy.Count == 0) return;
            Clipboard.SetDataObject(string.Join("\r\n", logsToCopy.Select(FormatLogLineForClipboard)));
        }

        private List<LogMessage> GetLogsToCopy()
        {
            if (SelectedLogs != null && SelectedLogs.Count > 0)
            {
                var selected = new HashSet<LogMessage>(SelectedLogs);
                return Logs.Where(l => selected.Contains(l)).ToList();
            }

            if (SelectedLog != null)
                return new List<LogMessage> { SelectedLog };

            return new List<LogMessage>();
        }

        private string FormatLogLineForClipboard(LogMessage logMessage)
        {
            return $"{logMessage.Time:yy-MM-dd HH:mm:ss.ffff};{logMessage.Level};{CheckNullableIntExists(logMessage.ProcessID)}{logMessage.Thread};{logMessage.Logger};{logMessage.Message}";
        }

        /// <summary>
        /// Очистить все логгеры
        /// </summary>
        private void ClearLoggers()
        {
            var isCheckedRoot = Loggers.First().IsChecked;
            var isExpandedRoot = Loggers.First().IsExpanded;

            Loggers.Clear();
            availableLoggers.Clear();
            exceptLoggers.Clear();
            exceptLoggersWithBuffer.Clear();
            showOnlyThisLoggers.Clear();

            toggledMarksCount = 0;

            Loggers.Add(new Node
            {
                Logger = "Root",
                Text = "Root",
                IsExpanded = isExpandedRoot,
                IsChecked = isCheckedRoot,
                Source = "-"
            });
        }

        /// <summary>
        /// Раскрыть все ветки ниже выбранной
        /// </summary>
        /// <param name="obj"></param>
        private void ExpandTreeViewChildren(object obj)
        {
            var node = obj as Node;
            if (node != null)
            {
                node.IsExpanded = true;
                if (node.Children.Any())
                    ExpandChild(node.Children.ToList());
            }
        }

        /// <summary>
        /// Свернуть все ветки до выбранной
        /// </summary>
        private void CollapseTreeViewChildren(object obj)
        {
            var node = obj as Node;
            if (node == null) return;

            if (node.Parent == null && node.Logger == "Root")
            {
                CollapseAllLoggers();
                return;
            }

            node.IsExpanded = false;
            if (node.Children.Any())
                CollapseChild(node.Children.ToList());
        }

        /// <summary>
        /// Свернуть все логгеры
        /// </summary>
        private void CollapseAllLoggers()
        {
            foreach (var child in Loggers[0].Children)
            {
                CollapseTreeViewChildren(child);
            }
        }

        private readonly HashSet<string> currentClearChildrenLoggers = new HashSet<string>();

        /// <summary>
        /// Очистка всей информации о дочерних элементах (пункт контектного меню в списке Loggers)
        /// </summary>
        /// <param name="obj"></param>
        private void ClearChildrenLoggers(object obj)
        {
            logger.Debug($"ClearChildrenLoggers with {obj}");
            currentClearChildrenLoggers.Clear();
            var node = obj as Node;
            if (node == null) return;
            try
            {
                IsVisibleLoader = true;
                var watched = FileWatchers.FirstOrDefault(x => x.FilePath.EndsWith(node.Text) || node.Logger != null && x.FilePath == node.Logger);
                if (watched != null)
                {
                    watched.Source?.Stop();
                    processingService.RemoveSource(watched.Source);
                    FileWatchers.Remove(watched);
                    OnPropertyChanged(nameof(FileWatchers));
                }

                if (node.Parent != null)
                {
                    UpdateLoggersAfterClear(node);
                    exceptLoggers = exceptLoggers.Except(currentClearChildrenLoggers).ToHashSet();
                    exceptLoggersWithBuffer = exceptLoggersWithBuffer.Except(currentClearChildrenLoggers).ToHashSet();
                    availableLoggers = availableLoggers.Except(currentClearChildrenLoggers).ToHashSet();
                    session.RemoveEntriesByLogger(node.Logger);
                    var parentNode = node.Parent;
                    parentNode.Children.Remove(node);
                }
                else
                {
                    ClearLoggers();
                    Clean();
                }

                if (importData.ContainsKey(node.Logger))
                    importData.Remove(node.Logger);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"An error occurred while ClearChildrenLoggers with {node}");
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                IsVisibleLoader = false;
            }
        }

        /// <summary>
        /// Обновляет информацию в списках доступных на текущий момент логгеров и списке исключенных для показа логгеров
        /// </summary>
        /// <param name="node"></param>
        private void UpdateLoggersAfterClear(Node node)
        {
            currentClearChildrenLoggers.Add(node.Logger);
            foreach (var nodeChild in node.Children)
            {
                UpdateLoggersAfterClear(nodeChild);
            }
        }

        /// <summary>
        /// Показывать только выбранный логгер
        /// </summary>
        /// <param name="obj"></param>
        private void ShowOnlyThisLogger(object obj)
        {
            logger.Debug($"ShowOnlyThisLogger with {obj}");
            var node = obj as Node;
            if (node == null) return;
            try
            {
                LastLogMessage = SelectedLog;
                IsVisibleLoader = true;
                exceptLoggers.Clear();
                exceptLoggersWithBuffer.Clear();
                CheckLoggers(node);
                if (node.Logger != "Root")
                    UncheckAllLoggers(Loggers.First(), node.Logger);
                exceptLoggers = availableLoggers.Except(showOnlyThisLoggers).ToHashSet();
                showOnlyThisLoggers.Clear();
                SyncFilterCriteriaToSession();
                _treeCheckJustDone = true;
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while ShowOnlyThisLogger.");
            }
            finally
            {
                IsVisibleLoader = false;
                SelectedLog = GetLastSelecterOrNearbyMessage();
            }
        }

        /// <summary>
        /// Скрыть логи от данного логгера и не получать их больше СОВСЕМ (не добавлять в буфер)
        /// </summary>
        /// <param name="obj"></param>
        private void DontReceiveThisLogger(object obj)
        {
            var node = SelectedNode;
            if (obj is Node paramNode)
                node = paramNode;

            if (node == null) return;

            node.IsChecked = false;
            exceptLoggers.Add(node.Logger);
            exceptLoggersWithBuffer.Add(node.Logger);

            if (node.Children.Any())
            {
                foreach (var nodeChild in node.Children)
                {
                    DontReceiveThisLogger(nodeChild);
                }
            }
        }

        /// <summary>
        /// Добавляем данный айпи в список игнорируемых
        /// </summary>
        /// <param name="obj"></param>
        private void IgnoreThisIP(object obj)
        {
            logger.Debug($"IgnoreThisIP with {obj}");
            var node = SelectedNode;
            if (obj is Node paramNode)
                node = paramNode;

            if (node == null || node.Parent == null && node.Text == "Root") return;

            HideSelectedNodeAndChild(node);

            while (!node.IsRoot)
            {
                node = node.Parent;
            }

            IPAddress.TryParse(node.Text, out IPAddress ip);

            if (ip == null)
                return;

            var existingIgnoreIP = Settings.Instance.IgnoredIPs.FirstOrDefault(x => x.IP == ip.ToString());
            if (existingIgnoreIP != null)
                existingIgnoreIP.IsActive = true;
            else
            {
                Settings.Instance.IgnoredIPs.Add(new IgnoredIPAddress
                {
                    IP = ip.ToString(),
                    IsActive = true
                });
            }

            // обновляем список игнорируемых IP - пересоздаем UDP источники с новым списком
            CreateUdpSourcesFromFactory();
            Settings.Instance.Save();
        }

        /// <summary>
        /// Не получать сообщения от данного логгера и не показывать его
        /// </summary>
        /// <param name="obj"></param>
        private void DontShowThisLogger(object obj)
        {
            try
            {
                if (SelectedNode != null)
                {
                    UncheckAndHideLoggers(SelectedNode);
                }
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while DontReceiveLogger");
            }
        }

        List<LogMessage> currentWarnLoggers = new List<LogMessage>();
        private LogMessage currentSearchLogger;
        private int warnSearchCounter = 0;
        private LogMessage prevSelectedWarnLog = new LogMessage();

        /// <summary>
        /// Поиск следующего варнинга
        /// </summary>
        private void FindNextWarning()
        {
            if (!currentWarnLoggers.Any() || warnSearchCounter >= currentWarnLoggers.Count || prevSelectedWarnLog != SelectedLog)
            {
                if (SelectedLog != null)
                {
                    warnSearchCounter = 0;
                    var selectedLogIndex = Logs.IndexOf(SelectedLog);
                        currentWarnLoggers = Logs.TakeLast(Logs.Count - selectedLogIndex).Where(x => x.Level == eLogLevel.Warn).ToList();
                }
                else
                        currentWarnLoggers = Logs.Where(x => x.Level == eLogLevel.Warn).ToList();
            }

            if (currentWarnLoggers.Any() && warnSearchCounter < currentWarnLoggers.Count)
            {
                currentSearchLogger = currentWarnLoggers[warnSearchCounter];
                warnSearchCounter++;
                SelectedLog = currentSearchLogger;
                prevSelectedWarnLog = SelectedLog;
            }
        }

        List<LogMessage> currentErrorLoggers = new List<LogMessage>();
        private int errorSearchCounter = 0;
        private LogMessage prevSelectedErrorLog = new LogMessage();

        /// <summary>
        /// Поиск следующей ошибки
        /// </summary>
        private void FindNextError()
        {
            if (!currentErrorLoggers.Any() || errorSearchCounter >= currentErrorLoggers.Count || prevSelectedErrorLog != SelectedLog)
            {
                if (SelectedLog != null)
                {
                    errorSearchCounter = 0;
                    var selectedLogIndex = Logs.IndexOf(SelectedLog);
                        currentErrorLoggers = Logs.TakeLast(Logs.Count - selectedLogIndex).Where(x => x.Level == eLogLevel.Error).ToList();
                }
                else
                        currentErrorLoggers = Logs.Where(x => x.Level == eLogLevel.Error).ToList();
            }

            if (currentErrorLoggers.Any() && errorSearchCounter < currentErrorLoggers.Count)
            {
                currentSearchLogger = currentErrorLoggers[errorSearchCounter];
                errorSearchCounter++;
                SelectedLog = currentSearchLogger;
                prevSelectedErrorLog = SelectedLog;
            }
        }

        private Dictionary<string, List<LogMessage>> importData = new Dictionary<string, List<LogMessage>>();
        private CancellationTokenSource cancelImportLogTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Загружает логи из файла (через Core ILogImportService). При отмене откатывает добавленные записи.
        /// </summary>
        /// <param name="obj">Файл, полученный через drag and drop</param>
        public async void ImportLogs(object obj)
        {
            List<ImportLogFile> importLogFiles = new List<ImportLogFile>();
            cancelImportLogTokenSource = new CancellationTokenSource();
            var extractDirectories = new List<string>();
            var extractedLogPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var selectedPaths = new List<string>();
                if (obj is IEnumerable<string> filesPath && filesPath.All(x => !string.IsNullOrEmpty(x) && File.Exists(x)))
                {
                    selectedPaths.AddRange(filesPath);
                }
                else if (obj is string logPath && !string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                {
                    selectedPaths.Add(logPath);
                }
                else
                {
                    OpenFileDialog fileDialog = new OpenFileDialog
                    {
                        Filter = "Logs and archives (*.txt;*.log;*.zip;*.rar)|*.txt;*.log;*.zip;*.rar|All files (*.*)|*.*",
                        Multiselect = true
                    };
                    if (fileDialog.ShowDialog() == true)
                        selectedPaths.AddRange(fileDialog.FileNames);
                }

                if (!selectedPaths.Any()) return;

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
                            logger.Error(ex, "Failed to extract archive {0}", path);
                            MessageBox.Show(string.Format(Locals.ArchiveExtractFailed, path, ex.Message), Locals.Error, MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show(Locals.ArchiveHasNoLogFiles, Locals.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                LogImportTemplateDialog logImportTemplateDialogDialog = new LogImportTemplateDialog(importLogFiles.First().FilePath);
                logImportTemplateDialogDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                logImportTemplateDialogDialog.ShowDialog();
                if (!logImportTemplateDialogDialog.DialogResult.HasValue || !logImportTemplateDialogDialog.DialogResult.Value)
                    return;

                var template = logImportTemplateDialogDialog.LogTemplate;
                Pause();

                List<WatchedFileInfo> currentFileWatchers = new List<WatchedFileInfo>();
                if (logImportTemplateDialogDialog.NeedUpdateFile)
                {
                    var dtoForTail = LogTemplateAdapter.ToDto(template);
                    if (dtoForTail != null)
                    {
                        foreach (var importLog in importLogFiles)
                        {
                            if (extractedLogPaths.Contains(importLog.FilePath))
                                continue;

                            if (FileWatchers.All(x => x.FilePath != importLog.FilePath))
                            {
                                try
                                {
                                    var fileSource = new FileLogSource(importLog.FilePath, dtoForTail, template.Encoding ?? "UTF-8");
                                    processingService.AddSource(fileSource);
                                    fileSource.Start();
                                    var watched = new WatchedFileInfo { FilePath = importLog.FilePath, Source = fileSource };
                                    currentFileWatchers.Add(watched);
                                }
                                catch { }
                            }
                        }
                    }
                }

                if (importLogFiles.Count > 1)
                {
                    ImportLogsProcessDialog importLogsProcessDialog = new ImportLogsProcessDialog(importLogFiles);
                    importLogsProcessDialog.Show();
                    importLogsProcessDialog.ImportProcessDialogResult += (sender, result) =>
                    {
                        if (!result) cancelImportLogTokenSource.Cancel();
                    };
                }

                var paths = importLogFiles.Select(x => x.FilePath).ToList();
                var dto = LogTemplateAdapter.ToDto(template);
                if (dto == null) return;

                IsVisibleProcessBar = true;
                var progress = new Progress<int>(p => ProcessBarValue = p);

                try
                {
                    await logImportService.ImportFromFilesAsync(paths, dto, progress, cancelImportLogTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    foreach (var path in paths)
                        session.RemoveEntriesBySource(path);
                    foreach (var importLogFile in importLogFiles)
                    {
                        var currentNode = Loggers[0].Children.FirstOrDefault(l => l.Logger == importLogFile.FilePath);
                        if (currentNode != null) ClearChildrenLoggers(currentNode);
                    }
                    foreach (var path in paths)
                    {
                        if (importData.ContainsKey(path)) importData.Remove(path);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "An error occurred while importing log files");
                    MessageBox.Show(string.Format("{0}\n{1}", Locals.IncorrectLogMessageTemplateMessageBoxInfo, ex.Message), Locals.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsVisibleProcessBar = false;
                    CleanIsEnabled = allLogs.Any();

                    if (logImportTemplateDialogDialog.NeedUpdateFile)
                    {
                        StartReadFromFileIsEnabled = false;
                        foreach (var w in currentFileWatchers)
                            FileWatchers.Add(w);
                        OnPropertyChanged(nameof(FileWatchers));
                    }

                    foreach (var path in extractedLogPaths)
                    {
                        if (importData.ContainsKey(path)) importData.Remove(path);
                    }
                }
            }
            finally
            {
                ArchiveLogExtractor.Cleanup(extractDirectories, logger);
            }
        }

        private void AddImportedLogFiles(IEnumerable<string> filesPath, List<ImportLogFile> importLogFiles)
        {
            foreach (var filePath in filesPath)
            {
                if (CheckFileExistsInImportLogs(filePath)) continue;
                AddImportedLogFile(filePath, importLogFiles);
            }
        }

        private void AddImportedLogFile(string filePath, List<ImportLogFile> importLogFiles)
        {
            importData.Add(filePath, new List<LogMessage>());
            importLogFiles.Add(new ImportLogFile
            {
                FilePath = filePath,
                Process = 0,
                FileName = Path.GetFileName(filePath)
            });
        }

        /// <summary>
        /// Выгружает в файл текущий отображаемый список логов (с учётом всех активных фильтров).
        /// Если команда вызвана из контекстного меню дерева логгеров, то экспортирует только отображаемые логи данного логгера.
        /// </summary>
        private void ExportLogs(object obj)
        {
            logger.Debug($"ExportLogs with {obj}");

            if (IsVisibleLoader) return;

            Pause();

            Node node = null;
            if (obj is Node paramNode)
                node = paramNode;

            string filePrefix = node == null ? string.Empty : $"{node.Text}_";
            SaveFileDialog saveDialog = new SaveFileDialog { DefaultExt = ".txt", Filter = "Text document|*.txt", FileName = $"LogViewerExportLog_{filePrefix}{DateTime.Now:yy-MM-dd}" };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    IsVisibleLoader = true;
                    IEnumerable<LogMessage> logsToExport = Logs;
                    if (node != null && node.Logger != "Root")
                        logsToExport = Logs.Where(x => x.FullPath.Contains(node.Logger));
                    var txtLogs = logsToExport.Select(logMessage => $"{logMessage.Time:yy-MM-dd HH:mm:ss.ffff};{logMessage.Level};{CheckNullableIntExists(logMessage.ProcessID)}{logMessage.Thread};{logMessage.Logger};{logMessage.Message}").ToList();
                    File.WriteAllLines(saveDialog.FileName, txtLogs, Encoding.UTF8);
                    Process.Start(Path.GetDirectoryName(saveDialog.FileName));
                }
                finally
                {
                    IsVisibleLoader = false;
                }
            }
        }

        /// <summary>
        /// Очищает результаты поиска
        /// </summary>
        private void ClearSearchResult()
        {
            if (IsVisibleLoader) return;

            if (IsSearchProcess)
            {
                IsSearchProcess = false;
                isTimeIntervalProcess = false;
                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList()
                    .Where(x => SelectedMinLogLevel.HasFlag(x.Level) && !exceptLoggers.Contains(x.FullPath)));
                SelectedLog = GetLastSelecterOrNearbyMessage();
            }

            SearchText = string.Empty;
            HighlightSearchText = string.Empty;
        }

        /// <summary>
        /// Поиск логгера в дереве
        /// </summary>
        private void SearchLoggers()
        {
            if (string.IsNullOrEmpty(SearchLoggerText))
            {
                ClearSearchLoggersResult();
                return;
            }
            LoggerHighlightText = SearchLoggerText;
            exceptParents.Clear();
            FindLastNodesAndUpdateVisibility(Loggers[0]);
            exceptParents.ForEach(x => x.IsVisible = true);
        }

        /// <summary>
        /// Очистка результатов поиска логгера
        /// </summary>
        private void ClearSearchLoggersResult()
        {
            if (isSearchLoggersProcess)
            {
                SetAllLoggersVisible(Loggers[0]);
                SearchLoggerText = string.Empty;
                LoggerHighlightText = string.Empty;
                isSearchLoggersProcess = false;
                IsEnableClearSearchLoggers = false;
            }
            SearchLoggerText = string.Empty;
            LoggerHighlightText = string.Empty;
        }

        /// <summary>
        /// Перейти к выбранной временной отметке
        /// </summary>
        private void GoToTimestamp()
        {
            if (IsVisibleLoader) return;

            SelectTimestampDialog selectTimestampDialog = new SelectTimestampDialog(SelectedLog?.Time);
            selectTimestampDialog.ShowDialog();

            if (selectTimestampDialog.DialogResult.HasValue && selectTimestampDialog.DialogResult.Value)
            {
                goToTimestampDateTime = selectTimestampDialog.PickedDateTime;
                TimeSpan truncateValue = TimeSpan.FromMinutes(1);

                if (goToTimestampDateTime.Second != 0)
                    truncateValue = TimeSpan.FromSeconds(1);
                if (goToTimestampDateTime.Millisecond != 0)
                    truncateValue = TimeSpan.FromMilliseconds(1);

                var firstTimestampLog = Logs.FirstOrDefault(x => x.Time.Truncate(truncateValue) == goToTimestampDateTime);
                if (firstTimestampLog != null)
                    SelectedLog = firstTimestampLog;
                else
                    MessageBox.Show(string.Format(Locals.NotFoundAnyMessagesWithDateMessageBoxInfo, goToTimestampDateTime.ToString(Settings.Instance.DataFormat)));
            }
        }

        private bool isTimeIntervalProcess = false;

        /// <summary>
        /// Установить интервал времени в пределах которого показывать логи
        /// </summary>
        private void SetTimeInterval()
        {
            if (IsVisibleLoader) return;

            SelectTimeIntervalDialog selectTimeIntervalDialog = new SelectTimeIntervalDialog(SelectedLog?.Time);
            selectTimeIntervalDialog.ShowDialog();
            if (selectTimeIntervalDialog.DialogResult.HasValue && selectTimeIntervalDialog.DialogResult.Value)
            {
                fromTimeInverval = selectTimeIntervalDialog.DateTimeFrom;
                toTimeInverval = selectTimeIntervalDialog.DateTimeTo;

                {
                    Logs = new AsyncObservableCollection<LogMessage>(Logs.Where(
                        x => x.Time >= fromTimeInverval &&
                             x.Time <= toTimeInverval));
                    IsSearchProcess = true;
                    isTimeIntervalProcess = true;
                    SyncFilterCriteriaToSession();
                }
            }
        }

        private int toggledMarksCount = 0;

        /// <summary>
        /// Выделить отдельным цветом сообщения от данного логгера
        /// </summary>
        /// <param name="obj"></param>
        private void ToggleMark(object obj)
        {
            var node = obj as Node;
            if (node != null)
            {
                var receiverColor = allLogs.FirstOrDefault(x => x.FullPath == node.Logger)?.Receiver?.Color?.Clone();

                bool isSet = false;

                SolidColorBrush currentColor;
                if (node.ToggleMark.Color.ToString() != TRANSPARENT_COLOR)
                {
                    toggledMarksCount--;

                    if (Settings.Instance.ShowMessageHighlightByReceiverColor)
                    {
                        if (receiverColor != null)
                        {
                            receiverColor.Opacity = 0.3;
                            currentColor = receiverColor;
                        }
                        else
                        {
                            currentColor = new SolidColorBrush(Colors.Transparent);
                        }
                    }
                    else
                    {
                        currentColor = new SolidColorBrush(Colors.Transparent);
                    }
                }
                else
                {
                    isSet = true;
                    toggledMarksCount++;
                    // назначаем выделение
                    Random rnd = new Random();
                    Color randomColor = Color.FromRgb((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
                    currentColor = new SolidColorBrush(randomColor);
                    currentColor.Opacity = 0.1;
                }

                node.ToggleMark = isSet ? currentColor : new SolidColorBrush(Colors.Transparent);

                {
                    foreach (var logMessage in Logs.Where(x => x.FullPath.Contains(node.Logger)))
                        logMessage.ToggleMark = currentColor;

                    foreach (var logMessage in allLogs.Where(x => x.FullPath.Contains(node.Logger)))
                        logMessage.ToggleMark = currentColor;
                }
            }
        }

        /// <summary>
        /// Поиск логгера в дереве по выбранному сообщению
        /// </summary>
        private void FindLoggerInTreeByMessage()
        {
            SearchLoggerText = SelectedLog.FullPath;
            exceptParents.Clear();
            FindLastNodesAndUpdateVisibility(Loggers[0], true);
            exceptParents.ForEach(x => x.IsVisible = true);
        }

        #endregion

        #region Работа с treeview

        List<string> showOnlyThisLoggers = new List<string>();

        private void CheckLoggers(Node node)
        {
            node.IsChecked = true;
            showOnlyThisLoggers.Add(node.Logger);
            if (node.Children.Any())
            {
                foreach (var nodeChild in node.Children)
                {
                    CheckLoggers(nodeChild);
                }
            }
        }

        /// <summary>
        /// Скрывает логгер выбранного сообщения
        /// </summary>
        /// <param name="node"></param>
        private void UncheckAndHideLoggers(Node node)
        {
            node.IsChecked = false;
            exceptLoggers.Add(node.Logger);
                Logs = new AsyncObservableCollection<LogMessage>(Logs.Where(l => !exceptLoggers.Contains(l.FullPath)));
        }

        /// <summary>
        /// Скрывает все дочерние логгеры выбранного сообщения 
        /// </summary>
        private void UncheckAllLoggers(Node node, string exceptLogger = null)
        {
            if (node.Logger == exceptLogger)
                return;

            node.IsChecked = false;
            if (node.Children.Any())
            {
                foreach (var nodeChild in node.Children)
                {
                    UncheckAllLoggers(nodeChild, exceptLogger);
                }
            }
        }

        private readonly List<string> currentExceptLoggers = new List<string>();

        /// <summary>
        /// Добавляет/удаляет все дочерние элементы из списка exceptLoggers
        /// </summary>
        /// <param name="node">Текущая нода</param>
        /// <param name="detele">Удалять или добавлять элемент</param>
        private void UpdateAllChildInExceptLoggers(Node node, bool detele = false)
        {
            var childNodes = node.Children;
            while (childNodes != null && childNodes.Any())
            {
                var currentNodes = childNodes.ToList();
                foreach (var childNode in currentNodes)
                {
                    if (detele)
                    {
                        exceptLoggers.Remove(childNode.Logger);
                        exceptLoggersWithBuffer.Remove(childNode.Logger);
                    }
                    else
                    {
                        currentExceptLoggers.Add(childNode.Logger);
                        exceptLoggers.Add(childNode.Logger);
                    }

                    if (childNode.Children.Any())
                        UpdateAllChildInExceptLoggers(childNode, detele);
                }
                childNodes = null;
            }
        }

        /// <summary>
        /// Раскрывает дочерние элементы
        /// </summary>
        /// <param name="nodes"></param>
        private void ExpandChild(List<Node> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = true;
                if (node.Children.Any())
                    ExpandChild(node.Children.ToList());
            }
        }

        /// <summary>
        /// Скрывает дочерние элементы
        /// </summary>
        /// <param name="nodes"></param>
        private void CollapseChild(List<Node> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = false;
                if (node.Children.Any())
                    CollapseChild(node.Children.ToList());
            }
        }

        /// <summary>
        /// Обновляет выбранный элемент дерева
        /// </summary>
        private void UpdateSelectedNode()
        {
            try
            {
                if (SelectedLog == null)
                    return;
                var nodes = string.IsNullOrEmpty(SelectedLog.ExecutableName) ?
                    new List<string> { SelectedLog.Address } :
                    new List<string> { SelectedLog.Address, SelectedLog.ExecutableName };

                nodes.AddRange(SelectedLog.Logger.Split('.'));

                if (!nodes.Any())
                    return;

                var foundNode = Loggers[0].Children.FirstOrDefault(x => x.Text == nodes[0] || x.Source == nodes[0]);
                if (foundNode == null) return;

                for (int i = 1; i < nodes.Count; i++)
                {
                    var node = foundNode.Children.FirstOrDefault(x => x.Text == nodes[i]);
                    if (node != null)
                    {
                        foundNode = node;
                    }
                }
                if (foundNode.Text != nodes.Last())
                    return;
                SelectedNode = foundNode;
            }
            catch (Exception e)
            {
                logger.Warn(e, $"An error occurred while UpdateSelectedNode. With {SelectedLog.FullPath}");
            }
        }

        /// <summary>
        /// Обновляет выделение на дереве
        /// </summary>
        private void UpdateNodeSelections(Node node, bool isSelect)
        {
            node.IsSelected = isSelect;
            if (node.Parent != null)
            {
                UpdateNodeSelections(node.Parent, isSelect);
            }
        }

        /// <summary>
        /// Снять чекбоксы с выделенного логгера и добавить их в игнор, убрать из списка
        /// </summary>
        /// <param name="node"></param>
        private void HideSelectedNodeAndChild(Node node)
        {
            logger.Debug($"HideSelectedNodeAndChild with {node.Text}");

            try
            {
                LastLogMessage = SelectedLog;
                IsVisibleLoader = true;

                UncheckAllLoggers(node);
                DontReceiveThisLogger(node);

                    Logs = node.Logger == "Root" ? new AsyncObservableCollection<LogMessage>() : new AsyncObservableCollection<LogMessage>(allLogs.Where(x => !exceptLoggers.Contains(x.FullPath)));

                showOnlyThisLoggers.Clear();
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while ShowOnlyThisLogger.");
            }
            finally
            {
                IsVisibleLoader = false;
                SelectedLog = GetLastSelecterOrNearbyMessage();
            }
        }

        private Node GetParentFromFullPath(Node root, string logger, string executableName = null)
        {
            List<string> nodesStr = new List<string>();
            if (!string.IsNullOrEmpty(executableName))
                nodesStr.Add(executableName);
            nodesStr.AddRange(logger.Split('.'));

            var parent = root;
            foreach (var node in nodesStr)
            {
                var prevParent = parent;
                parent = parent.Children.FirstOrDefault(x => x.Text == node);
                if (parent != null) continue;

                return prevParent;
            }

            return root;
        }

        /// <summary>
        /// Строит дерево логгеров по месседжу
        /// </summary>
        private void BuildTreeByMessage(LogMessage log, bool addLog = true)
        {
            // Если с таким IP не найден корневой элемент - создаем новое дерево.
            var root = Loggers[0].Children.FirstOrDefault(x => x.Text == log.Address);
            if (root == null)
            {
                var rootNode = new Node(Loggers[0], log.Address)
                {
                    IsRoot = true,
                    IsExpanded = true,
                    Logger = log.Address,
                    IsChecked = Loggers[0].IsChecked.HasValue && Loggers[0].IsChecked.Value,
                    IsVisible = !isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper()),
                    Source = log.Address,
                };
                Loggers[0].Children.Add(rootNode);
                root = rootNode;
            }

            if (toggledMarksCount > 0)
            {
                var currentNode = GetNodeFromMessage(log);
                log.ToggleMark = currentNode.ToggleMark;
            }

            if (availableLoggers.Contains(log.FullPath))
            {
                if (addLog)
                {
                    if (SelectedMinLogLevel.HasFlag(log.Level) && (!exceptLoggers.Contains(log.FullPath) && !exceptLoggersWithBuffer.Contains(log.FullPath)) && !IsSearchProcess
                        && (!isTimeIntervalProcess || isTimeIntervalProcess && log.Time > fromTimeInverval && log.Time < toTimeInverval))
                    {
Logs.Add(log);
                    }

                    if (!exceptLoggersWithBuffer.Contains(log.FullPath))
allLogs.Add(log);
                }

                CleanIsEnabled = allLogs.Any();
                return;
            }

            Node currentParent = GetParentFromFullPath(root, log.Logger, log.ExecutableName);

            if (Loggers[0].IsChecked.HasValue && !Loggers[0].IsChecked.Value ||
                currentParent.IsChecked.HasValue && !currentParent.IsChecked.Value || !currentParent.IsChecked.HasValue)
            {
                exceptLoggers.Add(log.FullPath);
                if (exceptLoggersWithBuffer.Contains(root.Parent.Logger))
                    exceptLoggersWithBuffer.Add(log.FullPath);
            }

            availableLoggers.Add(log.FullPath);

            if (addLog)
            {
                if (SelectedMinLogLevel.HasFlag(log.Level) && !exceptLoggers.Contains(log.FullPath) && !exceptLoggersWithBuffer.Contains(log.FullPath) && !IsSearchProcess)
Logs.Add(log);

                if (!exceptLoggersWithBuffer.Contains(log.FullPath))
allLogs.Add(log);
            }

            CleanIsEnabled = allLogs.Any();

            var nodes = log.Logger.Split('.').ToList();

            if (!string.IsNullOrEmpty(log.ExecutableName))
            {
                var exe = root.Children.FirstOrDefault(x => x.Text == log.ExecutableName);
                if (exe == null)
                {
                    var exeNode = new Node(root, log.ExecutableName)
                    {
                        IsChecked = root.IsChecked,
                        IsVisible = !isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper())
                    };
                    root.Children.Add(exeNode);
                    root = exeNode;
                }
                else
                    root = exe;
            }

            var parent = root;
            foreach (var node in nodes)
            {
                var prevParent = parent;
                parent = parent.Children.FirstOrDefault(x => x.Text == node);
                if (parent != null) continue;
                var newNode = new Node(prevParent, node)
                {
                    IsChecked = prevParent.IsChecked.HasValue && prevParent.IsChecked.Value,
                    IsVisible = !isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper()),
                };
                prevParent.Children.Add(newNode);
                parent = newNode;
            }
        }

        #region Поиск и очистка поиска логгеров в дерев

        private bool isSearchLoggersProcess = false;
        List<Node> exceptParents = new List<Node>();

        /// <summary>
        /// Обновляет видимость логгеров. 
        /// Используется при поискел логгеров
        /// </summary>
        /// <param name="node"></param>
        private void UpdateLoggersVisibility(Node node, bool fullPath = false)
        {
            if (exceptParents.Contains(node))
                return;

            if (fullPath && node.Logger.ToUpper().Contains(SearchLoggerText.ToUpper()) || CheckChildContainsText(node.Children, fullPath) ||
                node.Text.ToUpper().Contains(SearchLoggerText.ToUpper()))
            {
                node.IsVisible = true;
                AddParentsToExcept(node);
                return;
            }

            IsEnableClearSearchLoggers = true;
            isSearchLoggersProcess = true;

            node.IsVisible = false;
            if (node.Parent?.Parent != null)
            {
                UpdateLoggersVisibility(node.Parent, fullPath);
            }
        }

        /// <summary>
        /// Добавляет родителей найденного логгера в исключение.
        /// Необходимо для того, чтобы не выставлять родительским элементам visibility в false
        /// </summary>
        /// <param name="node"></param>
        private void AddParentsToExcept(Node node)
        {
            if (node == null || exceptParents.Contains(node)) return;
            exceptParents.Add(node);
            if (node.Parent != null) AddParentsToExcept(node.Parent);
        }

        private bool CheckChildContainsText(ObservableCollection<Node> nodeChildren, bool fullPath = false)
        {
            if (!nodeChildren.Any())
                return false;

            foreach (var nodeChild in nodeChildren)
            {
                if (fullPath && nodeChild.Logger.ToUpper().Contains(SearchLoggerText.ToUpper()) ||
                    nodeChild.Text.ToUpper().Contains(SearchLoggerText.ToUpper()))
                    return true;
                CheckChildContainsText(nodeChild.Children);
            }

            return false;
        }

        /// <summary>
        /// Находит конечные элементы дерева и начиная с них проставляет видимость логгерам
        /// </summary>
        public void FindLastNodesAndUpdateVisibility(Node node, bool fullPath = false)
        {
            if (node.Children.Any())
            {
                foreach (var child in node.Children)
                {
                    child.IsVisible = true;
                    FindLastNodesAndUpdateVisibility(child, fullPath);
                }
            }
            else
                UpdateLoggersVisibility(node, fullPath);
        }

        /// <summary>
        /// Делает все логгеры видимыми
        /// </summary>
        /// <param name="node"></param>
        private void SetAllLoggersVisible(Node node)
        {
            node.IsVisible = true;
            foreach (var nodeChild in node.Children)
            {
                SetAllLoggersVisible(nodeChild);
            }
        }

        #endregion

        /// <summary>
        /// Ищет элемент дерева, к которому относится данное сообщение
        /// </summary>
        private Node GetNodeFromMessage(LogMessage message)
        {
            Node currentNode = Loggers[0].Children.FirstOrDefault(x => x.Text == message.Address);
            Node foundNode = currentNode;

            List<string> nodesStr = new List<string>();
            if (!string.IsNullOrEmpty(message.ExecutableName))
                nodesStr.Add(message.ExecutableName);
            nodesStr.AddRange(message.Logger.Split('.'));

            foreach (var nodeName in nodesStr)
            {
                currentNode = currentNode.Children.FirstOrDefault(x => x.Text == nodeName);
                if (currentNode == null)
                    return foundNode;
                foundNode = currentNode;
            }
            return foundNode;
        }

        #endregion

        #region Остальные private методы

        private FilterCriteria CreateFilterCriteria(bool? isSearchActive = null)
        {
            return new FilterCriteria
            {
                MinLevel = (LogLevel)(int)SelectedMinLogLevel,
                ExcludedLoggerFullPaths = new HashSet<string>(exceptLoggers),
                ExcludedLoggerFullPathsWithBuffer = new HashSet<string>(exceptLoggersWithBuffer),
                SearchText = searchText,
                MatchCase = isMatchCase,
                MatchWholeWord = isMatchWholeWord,
                UseRegex = useRegularExpressions,
                MatchLogLevel = isMatchLogLevel,
                IsSearchActive = isSearchActive ?? isSearchProcess,
                IsTimeIntervalActive = isTimeIntervalProcess,
                TimeRangeFrom = fromTimeInverval,
                TimeRangeTo = toTimeInverval
            };
        }

        private void SyncFilterCriteriaToSession()
        {
            if (session?.FilterCriteria == null) return;
            processingService.SetFilterCriteria(CreateFilterCriteria());
        }

        /// <summary>
        /// Create UDP log sources from factory and add to processing service.
        /// </summary>
        private void CreateUdpSourcesFromFactory()
        {
            var results = udpSourceFactory.CreateFromSettings(receivers);
            if (results == null || !results.Any()) return;

            processingService.RemoveAllSources();
            udpSources.Clear();

            foreach (var result in results)
            {
                string errorMessage;
                if (!result.Source.TryInit(out errorMessage))
                {
                    MessageBox.Show(string.Format(Locals.PortIsBusy, result.Receiver.Port), Locals.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;
                }
                processingService.AddSource(result.Source);
                udpSources.Add(result.Source);
            }
        }

        private string CheckNullableIntExists(int? value)
        {
            return value.HasValue ? $"{value.Value};" : string.Empty;
        }

        /// <summary>
        /// Возвращает последний выбранный элемент или рядом стоящие с ним
        /// </summary>
        /// <returns></returns>
        private LogMessage GetLastSelecterOrNearbyMessage()
        {
            return Logs.Contains(LastLogMessage) ? LastLogMessage : nearbyLastLogMessages.FirstOrDefault(nearbyLastLogMessage => Logs.Contains(nearbyLastLogMessage));
        }

        /// <summary>
        /// Ищет рядом стоящее сообщение от текущего индекса по указанному уровню лога
        /// </summary>
        /// <param name="currentIndex">Текущий индекс</param>
        /// <param name="level">Уровень лога</param>
        private LogMessage FindNearMessageByLogLevel(int currentIndex, eLogLevel level)
        {
            if (Logs.Count <= currentIndex + 1 || currentIndex - 1 <= 0) return null;
            for (int i = currentIndex + 1; i < Logs.Count; i++)
            {
                if (Logs[i].Level == level)
                    return Logs[i];
            }

            for (int i = currentIndex - 1; i >= 0; i--)
            {
                if (Logs[i].Level == level)
                    return Logs[i];
            }

            return null;
        }

        /// <summary>
        /// Проверяет, есть ли данный файл в дикшионари importData или в наблюдаемых файлах
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns></returns>
        private bool CheckFileExistsInImportLogs(string filePath)
        {
            if (importData.ContainsKey(filePath) || FileWatchers.Any(x => x.FilePath == filePath))
            {
                MessageBox.Show(string.Format(Locals.FileAlreadyAdded, filePath));
                return true;
            }
            return false;
        }

        #endregion

        #region Обработчики событий

        /// <summary>
        /// Останавливает и удаляет все наблюдатели за файлами (Core FileLogSource).
        /// </summary>
        private void RemoveAllFileWatchers()
        {
            foreach (var w in FileWatchers)
            {
                w.Source?.Stop();
                processingService.RemoveSource(w.Source);
            }
            FileWatchers.Clear();
            OnPropertyChanged(nameof(FileWatchers));
        }

        #endregion

        public void Dispose()
        {
            RemoveAllFileWatchers();
            coreToUiAdapter?.Unsubscribe();
            processingService?.RemoveAllSources();
            cancellationToken?.Dispose();
        }
    }
}