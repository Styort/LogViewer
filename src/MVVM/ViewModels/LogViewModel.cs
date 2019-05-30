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

namespace LogViewer.MVVM.ViewModels
{
    public class LogViewModel : BaseViewModel, IDisposable
    {
        #region Поля

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly object logsLockObj = new object();
        private const int RECEIVER_COLUMN_WIDTH = 15;
        private const string TRANSPARENT_COLOR = "#00FFFFFF";

        private bool filterChanged = false;
        private readonly List<UDPPacketsParser> parsers;

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

        private string[] LogTypeArray = { ";Fatal;", ";Error;", ";Warn;", ";Trace;", ";Debug;", ";Info;" };
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

        public List<FileWatcher> FileWatchers { get; set; } = new List<FileWatcher>();

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
                OnPropertyChanged();

                Task.Run(() =>
                {
                    IsVisibleLoader = true;
                    LastLogMessage = SelectedLog;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        lock (logsLockObj)
                        {
                            if (IsSearchProcess)
                            {
                                var searchResult = GetSearchResult();
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Logs = new AsyncObservableCollection<LogMessage>(searchResult);
                                });
                            }
                            else
                            {
                                switch (SelectedMinLogLevel)
                                {
                                    case eLogLevel.Trace:
                                        Logs = new AsyncObservableCollection<LogMessage>(allLogs
                                            .Where(x => !exceptLoggers.Contains(x.FullPath)));
                                        break;
                                    case eLogLevel.Debug:
                                        Logs = new AsyncObservableCollection<LogMessage>(allLogs
                                            .Where(x => !x.Level.HasFlag(eLogLevel.Trace) && !exceptLoggers.Contains(x.FullPath)));
                                        break;
                                    case eLogLevel.Info:
                                        Logs = new AsyncObservableCollection<LogMessage>(allLogs
                                            .Where(x => !x.Level.HasFlag(eLogLevel.Debug) && !exceptLoggers.Contains(x.FullPath)));
                                        break;
                                    case eLogLevel.Warn:
                                        Logs = new AsyncObservableCollection<LogMessage>(allLogs
                                            .Where(x => !x.Level.HasFlag(eLogLevel.Info) && !exceptLoggers.Contains(x.FullPath)));
                                        break;
                                    case eLogLevel.Error:
                                        Logs = new AsyncObservableCollection<LogMessage>(allLogs
                                            .Where(x => !x.Level.HasFlag(eLogLevel.Warn) && !exceptLoggers.Contains(x.FullPath)));
                                        break;
                                    case eLogLevel.Fatal:
                                        Logs = new AsyncObservableCollection<LogMessage>(allLogs
                                            .Where(x => !x.Level.HasFlag(eLogLevel.Error) && !exceptLoggers.Contains(x.FullPath)));
                                        break;
                                }
                            }
                        }
                    });

                    IsVisibleLoader = false;
                    SelectedLog = GetLastSelecterOrNearbyMessage();
                });
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
            parsers = new List<UDPPacketsParser>();

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

            CreateParsers();

            ColorReceiverColumnWidth = receivers.Count == 1 || receivers.Where(r => r.IsActive).All(x => x.Color.Color == Colors.White) ? 0 : RECEIVER_COLUMN_WIDTH;

            if (Settings.Instance.AutoStartInStartup && !App.IsManualStartup)
                Start();
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
            if (!parsers.Any())
            {
                MessageBox.Show(Locals.NoReceiversMessageBoxInfo, Locals.Information,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            cancellationToken = new CancellationTokenSource();

            Task.Run(() =>
            {
                try
                {
                    foreach (var udpPacketsParser in parsers)
                    {
                        udpPacketsParser.Init();
                    }
                    if (parsers.All(x => !x.IsInitialized))
                        return;

                    StartIsEnabled = false;

                    Parallel.ForEach(parsers, parser =>
                    {
                        if (parser.IsInitialized)
                            ReadLogs(parser);
                    });
                }
                catch (Exception e)
                {
                    logger.Warn(e, "An error occurred while ReadLogs");
                }
            });
        }

        /// <summary>
        /// Остановка считывания логов
        /// </summary>
        private void Pause()
        {
            cancellationToken.Cancel();
            foreach (var udpPacketsParser in parsers)
            {
                if (udpPacketsParser.IsInitialized)
                    udpPacketsParser.Dispose();
            }
            StartIsEnabled = true;
        }

        /// <summary>
        /// Запустить считывание логов из файла
        /// </summary>
        private void StartFileReading()
        {
            StartReadFromFileIsEnabled = false;
            foreach (var fileWatcher in FileWatchers)
            {
                fileWatcher.StartWatch();
                fileWatcher.FileChanged += FileWatcherOnFileChanged;
            }
        }

        /// <summary>
        /// Остановить считывание логов из файла
        /// </summary>
        private void StopFileReading()
        {
            StartReadFromFileIsEnabled = true;
            foreach (var fileWatcher in FileWatchers)
            {
                fileWatcher.StopWatch();
                fileWatcher.FileChanged -= FileWatcherOnFileChanged;
            }
        }

        /// <summary>
        /// Очистка списка логов
        /// </summary>
        private void Clean()
        {
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

            var searchTask = Task.Run(() =>
            {
                try
                {
                    IsVisibleLoader = true;
                    // введённой значение пусто - возвращаем обратно весь список
                    if (string.IsNullOrEmpty(SearchText))
                    {
                        Application.Current.Dispatcher.Invoke(ClearSearchResult);
                        return;
                    }

                    LastLogMessage = SelectedLog;

                    currentSearch = SearchText;

                    bool isOpenInAnotherWinow = (bool)obj;

                    IsSearchProcess = !isOpenInAnotherWinow;

                    if (allLogs.Any())
                    {
                        // осуществляем поиск всему списку логов
                        IEnumerable<LogMessage> searchResult = GetSearchResult();

                        // открывать результат поиска в отдельном окне или нет
                        if (isOpenInAnotherWinow)
                        {
                            var logMessages = searchResult as List<LogMessage> ?? searchResult.ToList();
                            if (logMessages.Any())
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    SearchResult sr = new SearchResult(logMessages, SearchText, IsMatchCase);
                                    sr.Show();
                                    sr.ShowLogEvent += delegate (object sender, LogMessage message)
                                    {
                                        SelectedLog = message;
                                    };
                                });
                            }
                            else
                                MessageBox.Show(Locals.NothingFoundMessageBoxInfo, Locals.Search,
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            HighlightSearchText = SearchText;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Logs = new AsyncObservableCollection<LogMessage>(searchResult);
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Warn(e, "An error occurred while Search");
                }
            });
            searchTask.ContinueWith(x =>
            {
                IsVisibleLoader = false;
            });
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
            Task.Run(() =>
            {
                try
                {
                    IsVisibleLoader = true;

                    if (string.IsNullOrEmpty(SearchText) && SelectedLog != null)
                    {
                        SearchText = selectedLog.Message;
                        HighlightSearchText = SearchText;
                    }

                    if (!string.IsNullOrEmpty(SearchText))
                    {
                        HighlightSearchText = SearchText;

                        // если предыдущий запрос не такой же, то обнуляем счётчики - начинаем новый поиск
                        if (prevFindNext != SearchText || filterChanged)
                        {
                            filterChanged = false;
                            lastSelectedMessageCounter = 0;
                            prevFindNext = SearchText;
                            nextMessages = new List<LogMessage>();
                        }

                        // если счётчик найденных сообщений достиг количества ранее найденных сообщений по данному запросу,
                        // то делаем новую выборку - вдруг появилось то-нибудь новое
                        // или если выбран новый элемент в логе - поиск начинаем от него
                        if (lastSelectedMessageCounter >= nextMessages.Count || findNextPrevSelectedLog != SelectedLog)
                        {
                            if (SelectedLog != null)
                            {
                                lastSelectedMessageCounter = 0;
                                var selectedLogIndex = Logs.IndexOf(SelectedLog);
                                lock (logsLockObj)
                                    nextMessages = Logs.TakeLast(Logs.Count - selectedLogIndex - 1).ToList();
                            }

                            if (!nextMessages.Any())
                            {
                                lock (logsLockObj)
                                    nextMessages = IsMatchCase
                                        ? Logs.Where(x => x.Message.Contains(SearchText) || UseRegularExpressions && Regex.IsMatch(x.Message, SearchText)).ToList()
                                        : Logs.Where(x => x.Message.ToUpper().Contains(SearchText.ToUpper()) ||
                                                          UseRegularExpressions && Regex.IsMatch(x.Message, SearchText, RegexOptions.IgnoreCase)).ToList();
                            }
                            else
                                nextMessages = IsMatchCase
                                    ? nextMessages.Where(x => x.Message.Contains(SearchText) || UseRegularExpressions && Regex.IsMatch(x.Message, SearchText)).ToList()
                                    : nextMessages.Where(x => x.Message.ToUpper().Contains(SearchText.ToUpper()) ||
                                                              UseRegularExpressions && Regex.IsMatch(x.Message, SearchText, RegexOptions.IgnoreCase)).ToList();

                            if (!nextMessages.Any() || nextMessages.Count <= lastSelectedMessageCounter)
                            {
                                if (nextMessages.Count <= lastSelectedMessageCounter)
                                    return;
                                SelectedLog = null;
                                return;
                            }
                        }

                        SelectedLog = nextMessages[lastSelectedMessageCounter];
                        findNextPrevSelectedLog = SelectedLog;
                        lastSelectedMessageCounter++;
                    }
                }
                catch (Exception e)
                {
                    logger.Warn(e, "An error occurred while FindNext");
                }
            }).ContinueWith(x =>
            {
                IsVisibleLoader = false;
            });
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
            Task.Run(() =>
            {
                try
                {
                    IsVisibleLoader = true;

                    if (string.IsNullOrEmpty(SearchText) && SelectedLog != null)
                    {
                        SearchText = selectedLog.Message;
                        HighlightSearchText = SearchText;
                    }

                    if (!string.IsNullOrEmpty(SearchText))
                    {
                        HighlightSearchText = SearchText;
                        // если предыдущий запрос не такой же, то обнуляем счётчики - начинаем новый поиск
                        // или если один из фильтров изменился
                        if (prevFindPrevious != SearchText || filterChanged)
                        {
                            filterChanged = false;
                            prevFindPrevious = SearchText;
                            previousMessages = new List<LogMessage>();
                            lastSelectedPreviousMessageCounter = -1;
                        }

                        // если счётчик достиг 0, то усе - приехали
                        // или если выбран новый элемент в логе - поиск начинаем от него
                        if (lastSelectedPreviousMessageCounter == -1 || findPrevousPrevSelectedLog != SelectedLog)
                        {
                            var selectedLogIndex = Logs.IndexOf(SelectedLog);
                            lock (logsLockObj)
                                previousMessages = Logs.Take(selectedLogIndex).ToList();

                            if (previousMessages.Any())
                                previousMessages = IsMatchCase
                                    ? previousMessages.Where(x => x.Message.Contains(SearchText) || UseRegularExpressions && Regex.IsMatch(x.Message, SearchText)).ToList()
                                    : previousMessages.Where(x => x.Message.ToUpper().Contains(SearchText.ToUpper()) ||
                                                                  UseRegularExpressions && Regex.IsMatch(x.Message, SearchText, RegexOptions.IgnoreCase)).ToList();
                            else
                                return;

                            lastSelectedPreviousMessageCounter = previousMessages.Count - 1;

                            if (!previousMessages.Any() || lastSelectedPreviousMessageCounter == -1)
                                return;
                        }

                        SelectedLog = previousMessages[lastSelectedPreviousMessageCounter];
                        findPrevousPrevSelectedLog = SelectedLog;
                        lastSelectedPreviousMessageCounter--;
                    }
                }
                catch (Exception e)
                {
                    logger.Warn(e, "An error occurred while FindNext");
                }
            }).ContinueWith(x =>
            {
                IsVisibleLoader = false;
            });
        }

        /// <summary>
        /// Нажатие на чекбокс в списке классов (дереве)
        /// </summary>
        /// <param name="obj"></param>
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
                Task.Run(() =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            IEnumerable<LogMessage> remainLogs;
                            lock (logsLockObj)
                                remainLogs = allLogs.Where(x => SelectedMinLogLevel.HasFlag(x.Level) && !exceptLoggers.Contains(x.FullPath));
                            Logs = new AsyncObservableCollection<LogMessage>(remainLogs);
                        });
                    }
                    catch (Exception e)
                    {
                        logger.Warn(e, "An error occurred while TreeViewElementCheck");
                    }
                    finally
                    {
                        IsVisibleLoader = false;
                        SelectedLog = GetLastSelecterOrNearbyMessage();
                    }
                });
            }
            else
            {
                UpdateAllChildInExceptLoggers(node);
                currentExceptLoggers.Add(node.Logger);
                exceptLoggers.Add(node.Logger);
                Task.Run(() =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (node.Parent == null)
                            {
                                Logs.Clear();
                                return;
                            }

                            if (node.IsRoot)
                            {
                                lock (logsLockObj)
                                    Logs = new AsyncObservableCollection<LogMessage>(Logs.Where(x => x.Address != node.Source));
                                return;
                            }

                            IEnumerable<LogMessage> messagesForShow;
                            lock (logsLockObj)
                                messagesForShow = Logs.Where(x => !currentExceptLoggers.Contains(x.FullPath));

                            Logs = new AsyncObservableCollection<LogMessage>(messagesForShow);
                        });
                    }
                    catch (Exception e)
                    {
                        logger.Warn(e, "An error occurred while TreeViewElementCheck");
                    }
                    finally
                    {
                        IsVisibleLoader = false;
                        SelectedLog = GetLastSelecterOrNearbyMessage();
                    }
                });
            }
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
                Task.Run(() =>
                {
                    try
                    {
                        IsVisibleLoader = true;

                        allowMaxMessageBufferSize = Settings.Instance.IsEnabledMaxMessageBufferSize;
                        maxMessageBufferSize = Settings.Instance.MaxMessageBufferSize;
                        deletedMessagesCount = Settings.Instance.DeletedMessagesCount;
                        IsSourceVisible = Settings.Instance.IsShowSourceColumn;
                        IsThreadVisible = Settings.Instance.IsShowThreadColumn;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FontColor = FontColor.FromARGB(Settings.Instance.FontColor);
                        });

                        if (Settings.Instance.CurrentTheme != null && !Equals(Settings.Instance.CurrentTheme.Color, IconColor))
                        {
                            IconColor = Settings.Instance.CurrentTheme.Color;
                        }

                        // обновляем список игнорируемых IP в ресиверах
                        foreach (var receiver in parsers)
                        {
                            receiver.IgnoredIPs = Settings.Instance.IgnoredIPs;
                        }

                        // добавляем в действующие ресиверы те, которые были добавлены в настройках
                        foreach (var receiver in Settings.Instance.Receivers)
                        {
                            var foundReceiver = receivers.FirstOrDefault(x => x.Port == receiver.Port);
                            if (foundReceiver == null)
                            {
                                receivers.Add(receiver);
                                UDPPacketsParser parser = new UDPPacketsParser(receiver);
                                parsers.Add(parser);
                            }
                            else
                            {
                                // обновляем цвет ресивера
                                Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (foundReceiver.Color.Color != receiver.Color.Color)
                                {
                                    foundReceiver.Color = receiver.Color;

                                    lock (logsLockObj)
                                    {
                                        foreach (var logMessage in allLogs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                        {
                                            logMessage.Receiver.Color = foundReceiver.Color;
                                        }

                                        foreach (var logMessage in Logs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                        {
                                            logMessage.Receiver.Color = foundReceiver.Color;
                                        }
                                    }
                                }
                            });

                                // обновляем название ресивера
                                if (foundReceiver.Name != receiver.Name)
                                {
                                    foundReceiver.Name = receiver.Name;

                                    Parallel.ForEach(allLogs, log =>
                                    {
                                        log.Receiver.Name = foundReceiver.Name;
                                    });

                                    Parallel.ForEach(Logs, log =>
                                    {
                                        log.Receiver.Name = foundReceiver.Name;
                                    });
                                }
                            }
                        }

                        // удаляем из действующих ресиверов те, которые были убраны в настройках
                        foreach (var receiver in receivers.ToList())
                        {
                            var foundReceiver = Settings.Instance.Receivers.FirstOrDefault(x => x.Port == receiver.Port);
                            if (foundReceiver == null)
                            {
                                receivers.Remove(receiver);
                                var parcer = parsers.FirstOrDefault(x => x.Port == receiver.Port);
                                parcer?.Dispose();
                                parsers.Remove(parcer);
                            }
                        }

                        CreateParsers();

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
                });
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
            Task.Run(() =>
            {
                currentClearChildrenLoggers.Clear();
                var node = obj as Node;
                try
                {
                    IsVisibleLoader = true;

                    if (node != null)
                    {
                        var currentFileWatcher = FileWatchers.FirstOrDefault(x => x.FilePath.EndsWith(node.Text));
                        if (currentFileWatcher != null)
                        {
                            currentFileWatcher.StopWatch();
                            currentFileWatcher.FileChanged -= FileWatcherOnFileChanged;
                            FileWatchers.Remove(currentFileWatcher);
                            OnPropertyChanged(nameof(FileWatchers));
                        }

                        if (node.Parent != null)
                        {
                            UpdateLoggersAfterClear(node);

                            exceptLoggers = exceptLoggers.Except(currentClearChildrenLoggers).ToHashSet();
                            exceptLoggersWithBuffer = exceptLoggersWithBuffer.Except(currentClearChildrenLoggers).ToHashSet();
                            availableLoggers = availableLoggers.Except(currentClearChildrenLoggers).ToHashSet();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                lock (logsLockObj)
                                {
                                    allLogs = new AsyncObservableCollection<LogMessage>(allLogs.Where(x => !x.FullPath.Contains(node.Logger)));
                                    Logs = new AsyncObservableCollection<LogMessage>(Logs.Where(x => !x.FullPath.Contains(node.Logger)));
                                }
                                var parentNode = node.Parent;
                                parentNode.Children.Remove(node);
                            });
                        }
                        else
                        {
                            ClearLoggers();
                            Clean();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, $"An error occurred while ClearChildrenLoggers with {node}");
                }
            }).ContinueWith(x =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                IsVisibleLoader = false;
            });
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
            if (node != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        LastLogMessage = SelectedLog;
                        IsVisibleLoader = true;

                        exceptLoggers.Clear();
                        exceptLoggersWithBuffer.Clear();
                        CheckLoggers(node);

                        if (node.Logger == "Root")
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs);
                            });
                        }
                        else
                        {
                            UncheckAllLoggers(Loggers.First(), node.Logger);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                exceptLoggers = availableLoggers.Except(showOnlyThisLoggers).ToHashSet();
                                lock (logsLockObj)
                                    Logs = new AsyncObservableCollection<LogMessage>(allLogs.Where(x => x.FullPath.Contains(node.Logger)));
                            });
                        }

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
                });
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

            // обновляем список игнорируемых IP в ресиверах
            foreach (var receiver in parsers)
            {
                receiver.IgnoredIPs = Settings.Instance.IgnoredIPs;
            }
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
                    lock (logsLockObj)
                        currentWarnLoggers = Logs.TakeLast(Logs.Count - selectedLogIndex).Where(x => x.Level == eLogLevel.Warn).ToList();
                }
                else
                    lock (logsLockObj)
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
                    lock (logsLockObj)
                        currentErrorLoggers = Logs.TakeLast(Logs.Count - selectedLogIndex).Where(x => x.Level == eLogLevel.Error).ToList();
                }
                else
                    lock (logsLockObj)
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

        private List<LogMessage> importData = new List<LogMessage>();
        private string importFilePath = string.Empty;

        /// <summary>
        /// Загружает логи из файла
        /// </summary>
        /// <param name="obj">Файл, полученный через drag and drop</param>
        public void ImportLogs(object obj)
        {
            // выбираем файл
            importFilePath = string.Empty;

            if (obj is string filePath && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                importFilePath = filePath;
            }
            else
            {
                OpenFileDialog fileDialog = new OpenFileDialog { Filter = "Text files (*.txt;*.log)|*.txt;*.log| All files (*.*)|*.*" };
                if (fileDialog.ShowDialog() == true)
                    importFilePath = fileDialog.FileName;
                else
                    return;
            }

            // выбираем шаблон парсинга
            LogImportTemplateDialog logImportTemplateDialogDialog = new LogImportTemplateDialog(importFilePath);
            logImportTemplateDialogDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            logImportTemplateDialogDialog.ShowDialog();
            if (logImportTemplateDialogDialog.DialogResult.HasValue && logImportTemplateDialogDialog.DialogResult.Value)
            {
                var template = logImportTemplateDialogDialog.LogTemplate;

                UpdateLogTypeArray(template);

                Pause();
                FileWatcher fileWatcher = new FileWatcher();
                Task.Run(() =>
                {
                    IsVisibleProcessBar = true;

                    // считываем весь файл
                    try
                    {
                        using (FileStream stream = File.Open(importFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (logImportTemplateDialogDialog.NeedUpdateFile && FileWatchers.All(x => x.FilePath != importFilePath))
                            {
                                fileWatcher.FilePath = importFilePath;
                                fileWatcher.Position = stream.Length;
                                fileWatcher.Template = template;
                                FileWatchers.Add(fileWatcher);
                                OnPropertyChanged(nameof(FileWatchers));
                            }

                            var sb = new StringBuilder();
                            using (StreamReader sr = new StreamReader(stream, Encoding.GetEncoding(template.Encoding)))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    //проверяем, текущая запись - это новая запись или продолжение предыдущей.
                                    if (line.ContainsAnyOf(LogTypeArray))
                                    {
                                        if (line.Length != 0)
                                        {
                                            try
                                            {
                                                // парсим лог и добавляем в список
                                                LogParse(sb.ToString(), template);
                                                ProcessBarValue = (int)((double)sr.BaseStream.Position / sr.BaseStream.Length * 100);
                                            }
                                            catch (OutOfMemoryException ex)
                                            {
                                                logger.Error(ex, "An error occured while LogParse");
                                                throw;
                                            }
                                            catch (Exception e)
                                            {
                                                logger.Error(e, "An error occured while LogParse");
                                                MessageBox.Show(Locals.IncorrectLogMessageTemplateMessageBoxInfo);
                                                throw;
                                            }
                                            sb = new StringBuilder();
                                        }
                                        sb.Append(line);
                                    }
                                    else
                                    {
                                        sb.Append(Environment.NewLine);
                                        sb.Append(line);
                                    }
                                }
                                LogParse(sb.ToString(), template);
                            }
                        }

                        var allLogsTemp = allLogs.ToList();
                        allLogsTemp.AddRange(importData);
                        var logsTemp = Logs.ToList();
                        logsTemp.AddRange(importData.Where(l => SelectedMinLogLevel.HasFlag(l.Level) && !exceptLoggers.Contains(l.FullPath)));
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Logs = new AsyncObservableCollection<LogMessage>(logsTemp.OrderBy(x => x.Time));
                            allLogs = new AsyncObservableCollection<LogMessage>(allLogsTemp.OrderBy(x => x.Time));
                        });
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "An error occured while reading log file");
                    }
                    finally
                    {
                        importData.Clear();
                        CleanIsEnabled = allLogs.Any();
                        GC.Collect();
                        GC.WaitForFullGCComplete();
                        IsVisibleProcessBar = false;
                    }
                }).ContinueWith(x =>
                {
                    if (logImportTemplateDialogDialog.NeedUpdateFile)
                    {
                        StartReadFromFileIsEnabled = false;
                        fileWatcher.FileChanged += FileWatcherOnFileChanged;
                        fileWatcher.StartWatch();
                    }
                });
            }
        }

        /// <summary>
        /// Выгружает считанные логи в файл.
        /// Если команда вызвана из контекстного меню дерева логгеров, то экспортирует в файл только логи данного логгера.
        /// </summary>
        private void ExportLogs(object obj)
        {
            logger.Debug($"ExportLogs with {obj}");

            Pause();

            Node node = null;
            if (obj is Node paramNode)
                node = paramNode;

            string filePrefix = node == null ? string.Empty : $"{node.Text}_";
            SaveFileDialog saveDialog = new SaveFileDialog { DefaultExt = ".txt", Filter = "Text document|*.txt", FileName = $"LogViewerExportLog_{filePrefix}{DateTime.Now:yy-MM-dd}" };

            if (saveDialog.ShowDialog() == true)
            {
                Task.Run(() =>
                {
                    IsVisibleLoader = true;

                    // Если был выбран рут - то экспортим всё
                    var logsToExport = node == null || node.Logger == "Root"
                    ? allLogs
                    : allLogs.Where(x => x.FullPath.Contains(node.Logger));

                    List<string> txtLogs = logsToExport.Select(logMessage => $"{logMessage.Time:yy-MM-dd HH:mm:ss.ffff};{logMessage.Level};{CheckNullableIntExists(logMessage.ProcessID)}{logMessage.Thread};{logMessage.Address};{logMessage.Logger};{logMessage.Message}").ToList();

                    File.WriteAllLines(saveDialog.FileName, txtLogs, Encoding.UTF8);
                    IsVisibleLoader = false;

                    Process.Start(Path.GetDirectoryName(saveDialog.FileName));
                });
            }
        }

        /// <summary>
        /// Очищает результаты поиска
        /// </summary>
        private void ClearSearchResult()
        {
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
            SelectTimeIntervalDialog selectTimeIntervalDialog = new SelectTimeIntervalDialog(SelectedLog?.Time);
            selectTimeIntervalDialog.ShowDialog();
            if (selectTimeIntervalDialog.DialogResult.HasValue && selectTimeIntervalDialog.DialogResult.Value)
            {
                fromTimeInverval = selectTimeIntervalDialog.DateTimeFrom;
                toTimeInverval = selectTimeIntervalDialog.DateTimeTo;

                lock (logsLockObj)
                {
                    Logs = new AsyncObservableCollection<LogMessage>(Logs.Where(
                        x => x.Time >= fromTimeInverval &&
                             x.Time <= toTimeInverval));
                    IsSearchProcess = true;
                    isTimeIntervalProcess = true;
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
                SolidColorBrush currentColor;
                if (node.ToggleMark.Color.ToString() != TRANSPARENT_COLOR)
                {
                    toggledMarksCount--;
                    currentColor = new SolidColorBrush(Colors.Transparent);
                }
                else
                {
                    toggledMarksCount++;
                    // назначаем выделение
                    Random rnd = new Random();
                    Color randomColor = Color.FromRgb((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
                    currentColor = new SolidColorBrush(randomColor);
                }

                node.ToggleMark = currentColor;
                lock (logsLockObj)
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
            lock (logsLockObj)
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

                lock (logsLockObj)
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
            bool isImportFile = File.Exists(log.Address);

            // Если с таким IP не найден корневой элемент - создаем новое дерево.
            var root = Loggers[0].Children.FirstOrDefault(x => x.Text == (isImportFile ? Path.GetFileName(log.Address) : log.Address));
            if (root == null)
            {
                var rootNode = new Node(Loggers[0], isImportFile ? Path.GetFileName(log.Address) : log.Address)
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
                        lock (logsLockObj) Logs.Add(log);
                    }

                    if (!exceptLoggersWithBuffer.Contains(log.FullPath))
                        lock (logsLockObj) allLogs.Add(log);
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
                    lock (logsLockObj) Logs.Add(log);

                if (!exceptLoggersWithBuffer.Contains(log.FullPath))
                    lock (logsLockObj) allLogs.Add(log);
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

        /// <summary>
        /// Считывание логов
        /// </summary>
        private void ReadLogs(UDPPacketsParser parser)
        {
            while (true)
            {
                try
                {
                    if (cancellationToken.Token.IsCancellationRequested)
                        return;
                    var log = parser.GetLog();

                    // если учитывается максимальный буффер сообщений и он превыше - удаляем первое сообщение
                    if (allowMaxMessageBufferSize && (allLogs.Count >= maxMessageBufferSize || Logs.Count >= maxMessageBufferSize))
                    {
                        LastLogMessage = SelectedLog;

                        IsVisibleLoader = true;
                        if (allLogs.Count >= maxMessageBufferSize)
                        {
                            var temp = allLogs.ToList();
                            temp.RemoveRange(0, deletedMessagesCount);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                allLogs = new AsyncObservableCollection<LogMessage>(temp);
                            });
                        }

                        if (Logs.Count >= maxMessageBufferSize)
                        {
                            var temp = Logs.ToList();
                            temp.RemoveRange(0, deletedMessagesCount);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Logs = new AsyncObservableCollection<LogMessage>(temp);

                            });
                        }
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        IsVisibleLoader = false;

                        SelectedLog = GetLastSelecterOrNearbyMessage();
                    }

                    if (log != null)
                    {
                        var currentReceiver = receivers.FirstOrDefault(x => x.Port == log.Receiver.Port);
                        if (currentReceiver != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                log.Receiver.Color = currentReceiver.Color;
                                log.Receiver.Name = currentReceiver.Name;
                            });
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // если в конкретный момент идет процесс поиска, то добавляем в отображаемый список только то, что проходит условия поиска,
                            // а все кидаем в общий список
                            if (IsSearchProcess)
                            {
                                lock (logsLockObj) allLogs.Add(log);

                                if (isTimeIntervalProcess)
                                {
                                    if (log.Time > fromTimeInverval && log.Time < toTimeInverval)
                                        lock (logsLockObj) Logs.Add(log);
                                    return;
                                }

                                if (toggledMarksCount > 0)
                                {
                                    var currentNode = GetNodeFromMessage(log);
                                    log.ToggleMark = currentNode.ToggleMark;
                                }

                                if (!IsMatchCase && IsMatchLogLevel && SelectedMinLogLevel.HasFlag(log.Level)
                                    && log.Message.ToUpper().Contains(currentSearch.ToUpper()))
                                {
                                    lock (logsLockObj) Logs.Add(log);
                                    return;
                                }
                                if (IsMatchCase && IsMatchLogLevel && SelectedMinLogLevel.HasFlag(log.Level)
                                    && log.Message.Contains(currentSearch))
                                {
                                    lock (logsLockObj) Logs.Add(log);
                                    return;
                                }
                                if (IsMatchCase && !IsMatchLogLevel && log.Message.Contains(currentSearch))
                                {
                                    lock (logsLockObj) Logs.Add(log);
                                    return;
                                }
                                if (!IsMatchCase && !IsMatchLogLevel && log.Message.ToUpper().Contains(currentSearch.ToUpper()))
                                {
                                    lock (logsLockObj) Logs.Add(log);
                                    return;
                                }
                            }

                            BuildTreeByMessage(log);
                        });
                    }
                }
                catch (Exception e)
                {
                    logger.Warn(e, $"An error occurred while ReadLogs from port {parser.Port}");
                }
            }
        }

        /// <summary>
        /// Создаем экземпляры парсеров по текущим ресиверам
        /// </summary>
        private void CreateParsers()
        {
            if (!receivers.Any(r => r.IsActive)) return;

            if (parsers.Any())
            {
                foreach (var udpPacketsParser in parsers)
                {
                    udpPacketsParser.Dispose();
                }
                parsers.Clear();
            }

            foreach (var receiver in receivers.Where(r => r.IsActive))
            {
                var parser = new UDPPacketsParser(receiver);
                parsers.Add(parser);
            }
        }

        /// <summary>
        /// Считываем в конечный массив инфу из пришедших строк
        /// </summary>
        private void LogParse(String line, LogTemplate template)
        {
            if (string.IsNullOrEmpty(line))
                return;

            var log = line.Split(new[] { template.Separator }, StringSplitOptions.None);
            // собираем сообщение лога
            StringBuilder message = new StringBuilder();
            for (int i = template.TemplateParameterses[eImportTemplateParameters.message]; i < log.Length; i++)
            {
                if (!string.IsNullOrEmpty(log[i]))
                    message.Append(log[i] + "");
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                eImportTemplateParameters dataFormat = eImportTemplateParameters.date;

                if (template.TemplateParameterses.ContainsKey(eImportTemplateParameters.longdate))
                    dataFormat = eImportTemplateParameters.longdate;
                if (template.TemplateParameterses.ContainsKey(eImportTemplateParameters.shortdate))
                    dataFormat = eImportTemplateParameters.shortdate;
                if (template.TemplateParameterses.ContainsKey(eImportTemplateParameters.time))
                    dataFormat = eImportTemplateParameters.time;
                if (template.TemplateParameterses.ContainsKey(eImportTemplateParameters.ticks))
                    dataFormat = eImportTemplateParameters.ticks;

                var date = dataFormat != eImportTemplateParameters.ticks
                    ? DateTime.Parse(log[template.TemplateParameterses[dataFormat]].Replace("\0", ""))
                    : new DateTime(long.Parse(log[template.TemplateParameterses[dataFormat]].Replace("\0", "")));

                var lm = new LogMessage
                {
                    Address = importFilePath,
                    Time = date,
                    Level = LogLevelMapping[log[template.TemplateParameterses[eImportTemplateParameters.level]]],
                    Thread = template.TemplateParameterses.ContainsKey(eImportTemplateParameters.threadid) ? int.Parse(log[template.TemplateParameterses[eImportTemplateParameters.threadid]]) : -1,
                    Message = message.ToString(),
                    Logger = log[template.TemplateParameterses[eImportTemplateParameters.logger]],
                    ProcessID = template.TemplateParameterses.ContainsKey(eImportTemplateParameters.processid) ? int.Parse(log[template.TemplateParameterses[eImportTemplateParameters.processid]]) : (int?)null,
                };
                BuildTreeByMessage(lm, false);
                importData.Add(lm);
            });
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
        /// Обновляет массив с уровнем логгирования в зависимости от разделителя и от места, где пишется уровень лога
        /// </summary>
        private void UpdateLogTypeArray(LogTemplate template)
        {
            List<string> logTypeList = new List<string>();
            foreach (var logLevel in LogLevelMapping)
            {
                if (template.TemplateParameterses[eImportTemplateParameters.level] == 0)
                {
                    logTypeList.Add($"{template.Separator}{logLevel.Key}");
                }
                else if (template.TemplateParameterses[eImportTemplateParameters.level] ==
                         template.TemplateParameterses.Max(x => x.Value))
                {
                    logTypeList.Add($"{logLevel.Key}{template.Separator}");
                }
                else
                {
                    logTypeList.Add($"{template.Separator}{logLevel.Key}{template.Separator}");
                }
            }
            LogTypeArray = logTypeList.ToArray();
        }

        /// <summary>
        /// Осуществляет поиск с учетом всех фильтров
        /// </summary>
        private IEnumerable<LogMessage> GetSearchResult()
        {
            IEnumerable<LogMessage> searchResult = allLogs.ToList();

            if (!IsMatchCase && IsMatchLogLevel && !IsMatchWholeWord)
                return searchResult.Where(x => SelectedMinLogLevel.HasFlag(x.Level) &&
                                               (x.Message.ToUpper().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                               UseRegularExpressions && Regex.IsMatch(x.Message.ToUpper(), SearchText, RegexOptions.IgnoreCase)));
            if (IsMatchCase && IsMatchLogLevel && !IsMatchWholeWord)
                return searchResult.Where(x => SelectedMinLogLevel.HasFlag(x.Level) && 
                (x.Message.Contains(SearchText) ||
                 UseRegularExpressions && Regex.IsMatch(x.Message, SearchText)));

            if (IsMatchCase && !IsMatchLogLevel && !IsMatchWholeWord)
                return searchResult.Where(x => x.Message.Contains(SearchText) ||
                                               UseRegularExpressions && Regex.IsMatch(x.Message, SearchText));

            if (!IsMatchCase && !IsMatchLogLevel && !IsMatchWholeWord)
                return searchResult.Where(x => x.Message.ToUpper().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                               UseRegularExpressions && Regex.IsMatch(x.Message.ToUpper(), SearchText, RegexOptions.IgnoreCase));

            if (IsMatchWholeWord && IsMatchCase && IsMatchLogLevel)
                return searchResult.Where(x => SelectedMinLogLevel.HasFlag(x.Level) &&
                                               (x.Message.Contains($" {SearchText} ") ||
                                                x.Message.StartsWith($"{SearchText} ") ||
                                                x.Message.EndsWith($" {SearchText}") ||
                                                x.Message.StartsWith(SearchText) && x.Message.EndsWith(SearchText)));
            if (IsMatchWholeWord && !IsMatchCase && IsMatchLogLevel)
                return searchResult.Where(x => SelectedMinLogLevel.HasFlag(x.Level) &&
                                               (x.Message.Contains($" {SearchText} ", StringComparison.OrdinalIgnoreCase) ||
                                                x.Message.StartsWith($"{SearchText} ", StringComparison.OrdinalIgnoreCase) ||
                                                x.Message.EndsWith($" {SearchText}", StringComparison.OrdinalIgnoreCase) ||
                                                x.Message.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase) &&
                                                x.Message.EndsWith(SearchText, StringComparison.OrdinalIgnoreCase)));
            if (IsMatchWholeWord && IsMatchCase && !IsMatchLogLevel)
                return searchResult.Where(x => x.Message.Contains($" {SearchText} ") ||
                                               x.Message.StartsWith($" {SearchText}") ||
                                               x.Message.EndsWith($" {SearchText}") ||
                                               x.Message.StartsWith(SearchText) && x.Message.EndsWith(SearchText));
            if (IsMatchWholeWord && !IsMatchCase && !IsMatchLogLevel)
                return searchResult.Where(x => x.Message.Contains($" {SearchText} ", StringComparison.OrdinalIgnoreCase) ||
                                               x.Message.StartsWith($" {SearchText}", StringComparison.OrdinalIgnoreCase) ||
                                               x.Message.EndsWith($" {SearchText}", StringComparison.OrdinalIgnoreCase) ||
                                               x.Message.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase) &&
                                               x.Message.EndsWith(SearchText, StringComparison.OrdinalIgnoreCase));
            return searchResult;
        }

        /// <summary>
        /// Добавляем новые логи из файла
        /// </summary>
        private void UpdateLogsFromFile(FileWatcher watcher)
        {
            // TODO: Объединить данный метод с методом ImportLogs (часть чтения логов данного метода)
            try
            {
                using (FileStream stream = File.Open(watcher.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Position = watcher.Position;
                    var sb = new StringBuilder();
                    using (StreamReader sr = new StreamReader(stream, Encoding.GetEncoding(watcher.Template.Encoding)))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            //проверяем, текущая запись - это новая запись или продолжение предыдущей.
                            if (line.ContainsAnyOf(LogTypeArray))
                            {
                                if (line.Length != 0)
                                {
                                    try
                                    {
                                        // парсим лог и добавляем в список
                                        LogParse(sb.ToString(), watcher.Template);
                                    }
                                    catch (OutOfMemoryException ex)
                                    {
                                        logger.Error(ex, "An error occured while LogParse");
                                        throw;
                                    }
                                    catch (Exception e)
                                    {
                                        logger.Error(e, "An error occured while LogParse");
                                        MessageBox.Show(Locals.IncorrectLogMessageTemplateMessageBoxInfo);
                                        throw;
                                    }
                                    sb = new StringBuilder();
                                }
                                sb.Append(line);
                            }
                            else
                            {
                                sb.Append(Environment.NewLine);
                                sb.Append(line);
                            }
                        }
                        LogParse(sb.ToString(), watcher.Template);
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var logMessage in importData)
                        {
                            allLogs.Add(logMessage);
                            if (SelectedMinLogLevel.HasFlag(logMessage.Level) && !exceptLoggers.Contains(logMessage.FullPath))
                                Logs.Add(logMessage);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"An error occurred while UpdateLogsFromFile file - {watcher.FilePath}, position - {watcher.Position}");
            }
            finally
            {
                importData.Clear();
                CleanIsEnabled = allLogs.Any();
                GC.Collect();
                GC.WaitForFullGCComplete();
            }
        }

        #endregion

        #region Обработчики событий

        private void FileWatcherOnFileChanged(object sender, FileWatcher e)
        {
            UpdateLogsFromFile(e);
        }

        /// <summary>
        /// Очищает список наблюдателей за файлами и отписывается от событий
        /// </summary>
        private void RemoveAllFileWatchers()
        {
            foreach (var fileWatcher in FileWatchers)
            {
                fileWatcher.StopWatch();
                fileWatcher.FileChanged -= FileWatcherOnFileChanged;
            }
            FileWatchers.Clear();
            OnPropertyChanged(nameof(FileWatchers));
        }

        #endregion

        public void Dispose()
        {
            RemoveAllFileWatchers();

            foreach (var udpPacketsParser in parsers)
            {
                udpPacketsParser?.Dispose();
            }
            cancellationToken?.Dispose();
        }
    }
}