using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.TreeView;
using NLog;
using Application = System.Windows.Application;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Timer = System.Threading.Timer;

namespace LogViewer.MVVM.ViewModels
{
    public class LogViewModel : BaseViewModel, IDisposable
    {
        #region Поля

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const int RECEIVER_COLUMN_WIDTH = 15;
        private const int PERFORMANCE_TIMER_INTERVAL = 30000;

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

        // временная коллекция для осуществления поиска
        private AsyncObservableCollection<LogMessage> tempLogsList = new AsyncObservableCollection<LogMessage>();

        private AsyncObservableCollection<LogMessage> logs = new AsyncObservableCollection<LogMessage>();
        private CancellationTokenSource cancellationToken;
        private bool startIsEnabled = true;
        private bool pauseIsEnabled = false;
        private bool cleanIsEnabled = false;
        private bool clearSearchResultIsEnabled = false;
        private bool isVisibleLoader = false;
        private bool isVisibleProcessBar = false;
        private bool isEnableLogList = true;
        private bool isMatchCase = false;
        private bool isMatchLogLevel = true;
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
        private SolidColorBrush iconColor = (SolidColorBrush)new BrushConverter().ConvertFrom("#3F51B5");
        private SolidColorBrush fontColor = new SolidColorBrush(Colors.White);
        private bool isIpVisible = false;
        private bool isEnableClearSearchLoggers;
        private string searchLoggerText = string.Empty;

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


        /// <summary>
        /// Таймер отслеживания свободной памяти
        /// </summary>
        private Timer perfomarmanceTimer;

        private readonly string[] LogTypeArray = { ";Fatal;", ";Error;", ";Warn;", ";Trace;", ";Debug;", ";Info;" };
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

        /// <summary>
        /// Активность кнопки запуска считывания логов
        /// </summary>
        public bool StartIsEnabled
        {
            get => startIsEnabled;
            set
            {
                startIsEnabled = value;
                if (startIsEnabled)
                    PauseIsEnabled = false;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Активность кнопки паузы считывания логов
        /// </summary>
        public bool PauseIsEnabled
        {
            get => pauseIsEnabled;
            set
            {
                pauseIsEnabled = value;
                if (pauseIsEnabled)
                    StartIsEnabled = false;
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
                        switch (SelectedMinLogLevel)
                        {
                            case eLogLevel.Trace:
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList()
                                    .Where(x => !exceptLoggers.Contains(x.FullPath)));
                                break;
                            case eLogLevel.Debug:
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList()
                                    .Where(x => !x.Level.HasFlag(eLogLevel.Trace) && !exceptLoggers.Contains(x.FullPath)));
                                break;
                            case eLogLevel.Info:
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList()
                                    .Where(x => !x.Level.HasFlag(eLogLevel.Debug) && !exceptLoggers.Contains(x.FullPath)));
                                break;
                            case eLogLevel.Warn:
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList()
                                    .Where(x => !x.Level.HasFlag(eLogLevel.Info) && !exceptLoggers.Contains(x.FullPath)));
                                break;
                            case eLogLevel.Error:
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList()
                                    .Where(x => !x.Level.HasFlag(eLogLevel.Warn) && !exceptLoggers.Contains(x.FullPath)));
                                break;
                            case eLogLevel.Fatal:
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList()
                                    .Where(x => !x.Level.HasFlag(eLogLevel.Error) && !exceptLoggers.Contains(x.FullPath)));
                                break;
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
                if (isVisibleProcessBar) ProcessBarValue = 0;
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
                IsEnableFindPrevious = !string.IsNullOrEmpty(searchText) && SelectedLog != null;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Видимость колонки с IP
        /// </summary>
        public bool IsIpVisible
        {
            get => isIpVisible;
            set
            {
                isIpVisible = value;
                OnPropertyChanged(nameof(IpColumnWidth));
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Ширина колонки с IP
        /// </summary>
        public int IpColumnWidth => IsIpVisible ? 115 : 0;

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
                OnPropertyChanged();
            }
        }

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

            // TODO: Придумать что-нибудь с освобождением памяти, для предотвращения OutOfMemory
            //perfomarmanceTimer = new Timer(CheckPerformanceProcess, null, 0, PERFORMANCE_TIMER_INTERVAL);

            IconColor = Settings.Instance.CurrentTheme.Color;
            FontColor = FontColor.FromARGB(Settings.Instance.FontColor);
            allowMaxMessageBufferSize = Settings.Instance.IsEnabledMaxMessageBufferSize;
            maxMessageBufferSize = Settings.Instance.MaxMessageBufferSize;
            deletedMessagesCount = Settings.Instance.DeletedMessagesCount;
            IsIpVisible = Settings.Instance.IsShowIpColumn;

            receivers = Settings.Instance.Receivers;

            Loggers.Add(new Node
            {
                Logger = "Root",
                Text = "Root",
                IsExpanded = true,
                IsChecked = true
            });

            CreateParsers();

            ColorReceiverColumnWidth = receivers.Count == 1 || receivers.Where(r => r.IsActive).All(x => x.Color.Color == Colors.White) ? 0 : RECEIVER_COLUMN_WIDTH;

            #region Test

            //Loggers = new AsyncObservableCollection<Node>();
            //for (int i = 0; i < 3; i++)
            //{
            //    Loggers.Add(new Node
            //    {
            //        Text = $"CheckBox {i}",
            //        IsExpanded = true
            //    });
            //    for (int j = 0; j < 3; j++)
            //    {
            //        Loggers[i].Children.Add(new Node(Loggers[i].Parent, $"CheckBox {i} child {j}"));
            //        for (int k = 0; k < 3; k++)
            //        {
            //            Loggers[i].Children[j].Children.Add(new Node(Loggers[i].Children[j].Parent, $"CheckBox {i} child {j} child {k}"));
            //        }
            //    }
            //}

            //Array values = Enum.GetValues(typeof(eLogLevel));
            //Random random = new Random();
            //for (int i = 0; i < 100000; i++)
            //{
            //    var log = new LogMessage
            //    {
            //        Address = "192.168.1.54",
            //        Level = (eLogLevel)values.GetValue(random.Next(values.Length)),
            //        Logger = "Engy.Terminal.ViewModel.BlaBla",
            //        Message = $"{random.Next(10000)} keawda {random.Next(10000)}",
            //        Thread = 5,
            //        Time = DateTime.Now
            //    };

            //    //logsDictionary[log.Level].Add(log);

            //    Logs.Add(log);
            //}
            //allLogs = new AsyncObservableCollection<LogMessage>(Logs.ToList());

            #endregion

            if (Settings.Instance.AutoStartInStartup)
                Start();
        }

        #endregion

        #region Команды

        private RelayCommand startCommand;
        private RelayCommand pauseCommand;
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

        public RelayCommand StartCommand => startCommand ?? (startCommand = new RelayCommand(Start));
        public RelayCommand PauseCommand => pauseCommand ?? (pauseCommand = new RelayCommand(Pause));
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

        #endregion

        #region Обработчики команд

        /// <summary>
        /// Запуск считывания логов
        /// </summary>
        private void Start()
        {
            if (!parsers.Any())
            {
                MessageBox.Show("There are no receivers.\n Click \"Settings\" button to add new receivers.", "Info",
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
                    PauseIsEnabled = true;

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
        /// Очистка списка логов
        /// </summary>
        private void Clean()
        {
            Logs.Clear();
            nextMessages.Clear();
            tempLogsList.Clear();
            currentExceptLoggers.Clear();
            exceptLoggersWithBuffer.Clear();

            allLogs.Clear();
            lastSelectedMessageCounter = 0;
            prevFindNext = string.Empty;
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

                    ClearSearchResultIsEnabled = !isOpenInAnotherWinow || tempLogsList.Any();

                    tempLogsList = allLogs;

                    if (tempLogsList.Any())
                    {
                        // осуществляем поиск всему списку логов
                        IEnumerable<LogMessage> searchResult = tempLogsList.ToList();
                        if (!IsMatchCase && IsMatchLogLevel)
                        {
                            searchResult = searchResult.Where(x => SelectedMinLogLevel.HasFlag(x.Level) &&
                                                                   x.Message.ToUpper().Contains(SearchText,
                                                                       StringComparison.OrdinalIgnoreCase));
                        }
                        if (IsMatchCase && IsMatchLogLevel)
                        {
                            searchResult = searchResult.Where(x => SelectedMinLogLevel.HasFlag(x.Level) && x.Message.Contains(SearchText));
                        }
                        if (IsMatchCase && !IsMatchLogLevel)
                        {
                            searchResult = searchResult.Where(x => x.Message.Contains(SearchText));
                        }
                        if (!IsMatchCase && !IsMatchLogLevel)
                        {
                            searchResult = searchResult.Where(x => x.Message.ToUpper()
                                .Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                        }

                        // открывать результат поиска в отдельном окне или нет
                        if (isOpenInAnotherWinow)
                        {
                            var logMessages = searchResult as List<LogMessage> ?? searchResult.ToList();
                            if (logMessages.Any())
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Views.SearchResult sr = new Views.SearchResult(logMessages);
                                    sr.Show();
                                });
                            }
                            else
                                MessageBox.Show("Nothing found");
                        }
                        else
                        {
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
                    if (!string.IsNullOrEmpty(SearchText))
                    {
                        // если предыдущий запрос не такой же, то обнуляем счётчики - начинаем новый поиск
                        if (prevFindNext != SearchText)
                        {
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
                                nextMessages = Logs.ToList().TakeLast(Logs.Count - selectedLogIndex - 1).ToList();
                            }

                            if (!nextMessages.Any())
                                nextMessages = IsMatchCase
                                    ? Logs.ToList().Where(x => x.Message.Contains(SearchText)).ToList()
                                    : Logs.ToList().Where(x => x.Message.ToUpper().Contains(SearchText.ToUpper())).ToList();
                            else
                                nextMessages = IsMatchCase
                                    ? nextMessages.Where(x => x.Message.Contains(SearchText)).ToList()
                                    : nextMessages.Where(x => x.Message.ToUpper().Contains(SearchText.ToUpper())).ToList();

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
                    if (!string.IsNullOrEmpty(SearchText))
                    {
                        // если предыдущий запрос не такой же, то обнуляем счётчики - начинаем новый поиск
                        if (prevFindPrevious != SearchText)
                        {
                            prevFindPrevious = SearchText;
                            previousMessages = new List<LogMessage>();
                            lastSelectedPreviousMessageCounter = -1;
                        }

                        // если счётчик достиг 0, то усе - приехали
                        // или если выбран новый элемент в логе - поиск начинаем от него
                        if (lastSelectedPreviousMessageCounter == -1 || findPrevousPrevSelectedLog != SelectedLog)
                        {
                            var selectedLogIndex = Logs.IndexOf(SelectedLog);
                            previousMessages = Logs.ToList().Take(selectedLogIndex).ToList();

                            if (previousMessages.Any())
                                previousMessages = IsMatchCase
                                    ? previousMessages.Where(x => x.Message.Contains(SearchText)).ToList()
                                    : previousMessages.Where(x => x.Message.ToUpper().Contains(SearchText.ToUpper())).ToList();
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
                if (node.Text == "Root")
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
                            var remainLogs = allLogs.ToList().Where(x => SelectedMinLogLevel.HasFlag(x.Level) && !exceptLoggers.Contains(x.FullPath));

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
                            if (node.IsRoot)
                            {
                                Logs = new AsyncObservableCollection<LogMessage>(Logs.ToList().Where(x => x.Address != node.Text));
                                return;
                            }

                            IEnumerable<LogMessage> messagesForShow = Logs.ToList().Where(x => !currentExceptLoggers.Contains(x.FullPath));

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
            var isProgress = PauseIsEnabled;
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
                        IsIpVisible = Settings.Instance.IsShowIpColumn;

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
                                UDPPacketsParser parser = new UDPPacketsParser(receiver.Port);
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

                                    foreach (var logMessage in allLogs.ToList().Where(x => x.Receiver.Port == foundReceiver.Port))
                                    {
                                        logMessage.Receiver.Color = foundReceiver.Color;
                                    }

                                    foreach (var logMessage in Logs.ToList().Where(x => x.Receiver.Port == foundReceiver.Port))
                                    {
                                        logMessage.Receiver.Color = foundReceiver.Color;
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
            Loggers.Add(new Node
            {
                Logger = "Root",
                Text = "Root",
                IsExpanded = isExpandedRoot,
                IsChecked = isCheckedRoot
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
                        if (node.Parent != null)
                        {
                            UpdateLoggersAfterClear(node);

                            exceptLoggers = exceptLoggers.Except(currentClearChildrenLoggers).ToHashSet();
                            exceptLoggersWithBuffer = exceptLoggersWithBuffer.Except(currentClearChildrenLoggers).ToHashSet();
                            availableLoggers = availableLoggers.Except(currentClearChildrenLoggers).ToHashSet();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                allLogs = new AsyncObservableCollection<LogMessage>(allLogs.ToList().Where(x => !x.FullPath.Contains(node.Logger)));
                                Logs = new AsyncObservableCollection<LogMessage>(Logs.ToList().Where(x => !x.FullPath.Contains(node.Logger)));
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
                                Logs = new AsyncObservableCollection<LogMessage>(allLogs.ToList().Where(x => x.FullPath.Contains(node.Logger)));
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
                    currentWarnLoggers = Logs.TakeLast(Logs.Count - selectedLogIndex).Where(x => x.Level == eLogLevel.Warn).ToList();
                }
                else
                    currentWarnLoggers = Logs.ToList().Where(x => x.Level == eLogLevel.Warn).ToList();
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
                    currentErrorLoggers = Logs.ToList().Where(x => x.Level == eLogLevel.Error).ToList();
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
            Views.LogImportTemplate logImportTemplateDialog = new Views.LogImportTemplate();
            logImportTemplateDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            logImportTemplateDialog.ShowDialog();
            if (logImportTemplateDialog.DialogResult.HasValue && logImportTemplateDialog.DialogResult.Value)
            {
                var template = logImportTemplateDialog.TemplateParameterses;

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

                Pause();

                Task.Run(() =>
                {
                    IsVisibleProcessBar = true;

                    // считываем весь файл
                    try
                    {
                        var sb = new StringBuilder();
                        using (StreamReader sr = new StreamReader(importFilePath, Encoding.GetEncoding("Windows-1251")))
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
                                            MessageBox.Show("Incorrect log message template!");
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
                        CleanIsEnabled = Logs.Any();
                        GC.Collect();
                        GC.WaitForFullGCComplete();
                        IsVisibleProcessBar = false;
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

                    List<string> txtLogs = logsToExport.Select(logMessage => $"{logMessage.Time:yy-MM-dd HH:mm:ss.ffff};{logMessage.Level};{CheckNullableIntExists(logMessage.EventID)}{CheckNullableIntExists(logMessage.ProcessID)}{logMessage.Thread};{logMessage.Address};{logMessage.Logger};{logMessage.Message}").ToList();

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
            ClearSearchResultIsEnabled = false;
            SearchText = string.Empty;
            if (!tempLogsList.Any()) return;
            Logs = new AsyncObservableCollection<LogMessage>(tempLogsList.ToList()
                .Where(x => SelectedMinLogLevel.HasFlag(x.Level) && !exceptLoggers.Contains(x.FullPath)));
            tempLogsList = new AsyncObservableCollection<LogMessage>();
            SelectedLog = GetLastSelecterOrNearbyMessage();
        }

        /// <summary>
        /// Поиск логгера в дереве
        /// </summary>
        private void SearchLoggers()
        {
            exceptParents.Clear();
            FindLastNodesAndUpdateVisibility(Loggers[0]);
            exceptParents.ForEach(x => x.IsVisible = true);
        }

        /// <summary>
        /// Очистка результатов поиска логгера
        /// </summary>
        private void ClearSearchLoggersResult()
        {
            SetAllLoggersVisible(Loggers[0]);
            IsEnableClearSearchLoggers = false;
            SearchLoggerText = string.Empty;
            isSearchLoggersProcess = false;
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
            Logs = new AsyncObservableCollection<LogMessage>(Logs.ToList().Where(l => !exceptLoggers.Contains(l.FullPath)));
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

                var foundNode = Loggers[0].Children.FirstOrDefault(x => x.Text == nodes[0]);
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

                Logs = node.Logger == "Root" ? new AsyncObservableCollection<LogMessage>() : new AsyncObservableCollection<LogMessage>(allLogs.ToList().Where(x => !exceptLoggers.Contains(x.FullPath)));

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
                    IsChecked = (Loggers[0].IsChecked.HasValue && Loggers[0].IsChecked.Value),
                    IsVisible = !isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper())
                };
                Loggers[0].Children.Add(rootNode);
                root = rootNode;
            }

            if (availableLoggers.Contains(log.FullPath))
            {
                if (addLog)
                {
                    if (SelectedMinLogLevel.HasFlag(log.Level) && (!exceptLoggers.Contains(log.FullPath) && !exceptLoggersWithBuffer.Contains(log.FullPath)) && !ClearSearchResultIsEnabled)
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
                if (SelectedMinLogLevel.HasFlag(log.Level) && !exceptLoggers.Contains(log.FullPath) && !exceptLoggersWithBuffer.Contains(log.FullPath) && !ClearSearchResultIsEnabled)
                    Logs.Add(log);

                if (!exceptLoggersWithBuffer.Contains(log.FullPath))
                    allLogs.Add(log);
            }

            CleanIsEnabled = Logs.Any();

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
                    IsVisible = !isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper())
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
        private void UpdateLoggersVisibility(Node node)
        {
            if (exceptParents.Contains(node))
                return;

            if (node.Text.ToUpper().Contains(SearchLoggerText.ToUpper()) || CheckChildContainsText(node.Children))
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
                UpdateLoggersVisibility(node.Parent);
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

        private bool CheckChildContainsText(ObservableCollection<Node> nodeChildren)
        {
            if (!nodeChildren.Any())
                return false;

            foreach (var nodeChild in nodeChildren)
            {
                if (nodeChild.Text.ToUpper().Contains(SearchLoggerText.ToUpper()))
                    return true;
                CheckChildContainsText(nodeChild.Children);
            }

            return false;
        }

        /// <summary>
        /// Находит конечные элементы дерева и начиная с них проставляет видимость логгерам
        /// </summary>
        public void FindLastNodesAndUpdateVisibility(Node node)
        {
            if (node.Children.Any())
            {
                foreach (var child in node.Children)
                {
                    child.IsVisible = true;
                    FindLastNodesAndUpdateVisibility(child);
                }
            }
            else
                UpdateLoggersVisibility(node);
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
                            if (ClearSearchResultIsEnabled)
                            {
                                allLogs.Add(log);

                                if (!IsMatchCase && IsMatchLogLevel && SelectedMinLogLevel.HasFlag(log.Level)
                                    && log.Message.ToUpper().Contains(currentSearch.ToUpper()))
                                {
                                    Logs.Add(log);
                                    return;
                                }
                                if (IsMatchCase && IsMatchLogLevel && SelectedMinLogLevel.HasFlag(log.Level)
                                    && log.Message.Contains(currentSearch))
                                {
                                    Logs.Add(log);
                                    return;
                                }
                                if (IsMatchCase && !IsMatchLogLevel && log.Message.Contains(currentSearch))
                                {
                                    Logs.Add(log);
                                    return;
                                }
                                if (!IsMatchCase && !IsMatchLogLevel && log.Message.ToUpper().Contains(currentSearch.ToUpper()))
                                {
                                    Logs.Add(log);
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
                var parser = new UDPPacketsParser(receiver.Port);
                parsers.Add(parser);
            }
        }

        private void CheckPerformanceProcess(object state)
        {
            Int64 tot = PerformanceInfo.GetTotalMemoryInMiB();
            var gcAppUsageMemory = GC.GetTotalMemory(false) / 1048576;

            logger.Debug($"CheckPerformanceProcess: GC App RAM memory usage - {gcAppUsageMemory}");

            // TODO: Проверить объём, занимаемый самим приложением, и в условие пихать его тоже.
            if (gcAppUsageMemory > 2048 || gcAppUsageMemory > tot / 2)
            {
                PerformanceLogCleanup();
            }
        }

        private bool isCleanupProcess = false;
        private void PerformanceLogCleanup()
        {
            if (isCleanupProcess) return;
            logger.Debug("PerformanceLogCleanup");
            isCleanupProcess = true;

            Pause();

            IsVisibleLoader = true;

            var allLogsDeletedCount = (int)(allLogs.Count * 0.3);
            var logsDeletedCount = (int)(Logs.Count * 0.3);

            var tempAllLogs = allLogs.ToList();
            tempAllLogs.RemoveRange(0, allLogsDeletedCount);
            allLogs = new AsyncObservableCollection<LogMessage>(tempAllLogs);

            var tempLogs = Logs.ToList();
            tempLogs.RemoveRange(0, logsDeletedCount);
            Logs = new AsyncObservableCollection<LogMessage>(tempLogs);

            tempLogs.Clear();
            tempAllLogs.Clear();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            IsVisibleLoader = false;
            Start();
            isCleanupProcess = false;
        }

        /// <summary>
        /// Считываем в конечный массив инфу из пришедших строк
        /// </summary>
        private void LogParse(String line, Dictionary<eImportTemplateParameters, int> template)
        {
            if (string.IsNullOrEmpty(line))
                return;

            var log = line.Split(';');
            // собираем сообщение лога
            StringBuilder message = new StringBuilder();
            for (int i = template[eImportTemplateParameters.Message]; i < log.Length; i++)
            {
                if (!string.IsNullOrEmpty(log[i]))
                    message.Append(log[i] + "");
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                var lm = new LogMessage
                {
                    Address = $"Import ({importFilePath})",
                    Time = DateTime.Parse(log[template[eImportTemplateParameters.DateTime]].Replace("\0", "")),
                    Level = LogLevelMapping[log[template[eImportTemplateParameters.LogLevel]]],
                    Thread = template.ContainsKey(eImportTemplateParameters.ThreadNumber) ? int.Parse(log[template[eImportTemplateParameters.ThreadNumber]]) : -1,
                    Message = message.ToString(),
                    Logger = log[template[eImportTemplateParameters.Logger]],
                    ProcessID = template.ContainsKey(eImportTemplateParameters.ProcessID) ? int.Parse(log[template[eImportTemplateParameters.ProcessID]]) : (int?)null,
                    EventID = template.ContainsKey(eImportTemplateParameters.EventID) ? int.Parse(log[template[eImportTemplateParameters.EventID]]) : (int?)null,
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

        #endregion

        public void Dispose()
        {
            foreach (var udpPacketsParser in parsers)
            {
                udpPacketsParser?.Dispose();
            }
            cancellationToken?.Dispose();
            perfomarmanceTimer?.Dispose();
        }
    }
}