using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using NLog;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.Views;

namespace LogViewer.MVVM.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private ObservableCollection<Receiver> receivers = new ObservableCollection<Receiver>();
        private ObservableCollection<IgnoredIPAddress> ignoredIpAdresses = new ObservableCollection<IgnoredIPAddress>();
        private bool isConfigurationVisible = false;
        private bool isAutoStartReadAtStartup = true;
        private bool minimizeToTray = false;
        private bool isEnableMaxMessageBufferSize = false;
        private bool onlyOneAppInstance = true;
        private Receiver selectedReceiver;
        private string displayedDataFormat = "dd/MM/yyyy HH:mm:ss.fff";
        private IgnoredIPAddress selectedIP;
        private string typedIP;
        private string selectedDataFormat;
        private CultureInfo selectedLanguage;
        private int maxMessageBufferSize;
        private int deletedMessagesCount;
        private SolidColorBrush fontColor = new SolidColorBrush(Colors.White);
        private bool isShowSourceColumn;
        private bool isShowThreadColumn;
        private bool isShowTaskbarProgress;
        private bool isShowErrorTimeline;
        private bool showMessageHighlightByReceiverColor;
        private bool isSeparateIpLoggersByPort;
        private string selectedMessageFontFamily = "Consolas";
        private double messageFontSize = 14;

        #region Properties

        public ObservableCollection<Receiver> Receivers
        {
            get => receivers;
            set
            {
                receivers = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<IgnoredIPAddress> IgnoredIpAdresses
        {
            get => ignoredIpAdresses;
            set
            {
                ignoredIpAdresses = new AsyncObservableCollection<IgnoredIPAddress>(value.OrderBy(x => x.IP));
                OnPropertyChanged();
            }
        }

        public bool IsConfigurationVisible
        {
            get => isConfigurationVisible;
            set
            {
                isConfigurationVisible = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Start receiving logs at application startup.
        /// </summary>
        public bool IsAutoStartReadAtStartup
        {
            get => isAutoStartReadAtStartup;
            set
            {
                isAutoStartReadAtStartup = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Show a tray icon when the window is minimized.
        /// </summary>
        public bool MinimizeToTray
        {
            get => minimizeToTray;
            set
            {
                minimizeToTray = value;
                OnPropertyChanged();
            }
        }


        /// <summary>
        /// Honor the maximum message buffer size.
        /// </summary>
        public bool IsEnableMaxMessageBufferSize
        {
            get => isEnableMaxMessageBufferSize;
            set
            {
                isEnableMaxMessageBufferSize = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Maximum number of received logs.
        /// </summary>
        public int MaxMessageBufferSize
        {
            get => maxMessageBufferSize;
            set
            {
                maxMessageBufferSize = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// How many messages to delete when the buffer overflows.
        /// </summary>
        public int DeletedMessagesCount
        {
            get => deletedMessagesCount;
            set
            {
                if (value >= MaxMessageBufferSize)
                    deletedMessagesCount = MaxMessageBufferSize;
                if (value <= 0)
                    deletedMessagesCount = 1;
                if (value > 0 && value < MaxMessageBufferSize)
                    deletedMessagesCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Allow only one application instance.
        /// </summary>
        public bool OnlyOneAppInstance
        {
            get => onlyOneAppInstance;
            set
            {
                onlyOneAppInstance = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Displayed date format.
        /// </summary>
        public string DisplayedDataFormat
        {
            get => displayedDataFormat;
            set
            {
                displayedDataFormat = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// IP typed in the ignore box.
        /// </summary>
        public string TypedIP
        {
            get => typedIP;
            set
            {
                typedIP = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Selected ignored IP.
        /// </summary>
        public IgnoredIPAddress SelectedIP
        {
            get => selectedIP;
            set
            {
                selectedIP = value;
                OnPropertyChanged();
            }
        }

        public Receiver SelectedReceiver
        {
            get => selectedReceiver;
            set
            {
                selectedReceiver = value;
                IsConfigurationVisible = selectedReceiver != null;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Current font color.
        /// </summary>
        public SolidColorBrush FontColor { get; set; } = new SolidColorBrush();

        /// <summary>
        /// Selected font color.
        /// </summary>
        public SolidColorBrush SelectedFontColor
        {
            get => fontColor;
            set
            {
                fontColor = value;
                OnPropertyChanged();
            }
        }

        public string ExampleDateTime { get; set; }

        public List<string> DataFormats { get; set; } = new List<string>
        {
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss.fff",
            "HH:mm",
            "HH:mm:ss",
            "HH:mm:ss.fff",
            "dd/MM/yyyy"
        };

        public Dictionary<CultureInfo, string> Languages { get; set; } = new Dictionary<CultureInfo, string>
        {
            {new CultureInfo("en"), "English"},
            {new CultureInfo("ru"), "Русский"},
        };

        public CultureInfo SelectedLanguage
        {
            get => selectedLanguage;
            set
            {
                selectedLanguage = value;
                OnPropertyChanged();
            }
        }

        public string SelectedDataFormat
        {
            get => selectedDataFormat;
            set
            {
                selectedDataFormat = value;
                ExampleDateTime = DateTime.Now.ToString(selectedDataFormat);
                OnPropertyChanged(nameof(ExampleDateTime));
                OnPropertyChanged();
            }
        }

        public List<Theme> Themes { get; set; } = new List<Theme>
        {
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#FFEB3B"),
                Name = "Yellow"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#FFC107"),
                Name = "Amber"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#FF5722"),
                Name = "DeepOrange"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#03A9F4"),
                Name = "LightBlue"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#009688"),
                Name = "Teal"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#00BCD4"),
                Name = "Cyan"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#E91E63"),
                Name = "Pink"
            },            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#4CAF50"),
                Name = "Green"
            },            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#673AB7"),
                Name = "DeepPurple"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#3F51B5"),
                Name = "Indigo"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#8BC34A"),
                Name = "LightGreen"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#2196F3"),
                Name = "Blue"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#CDDC39"),
                Name = "Lime"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#F44336"),
                Name = "Red"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#FF9800"),
                Name = "Orange"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#9C27B8"),
                Name = "Purple"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#607D8B"),
                Name = "BlueGrey"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#9E9E9E"),
                Name = "Grey"
            },
            new Theme
            {
                Color = (SolidColorBrush) new BrushConverter().ConvertFrom("#795548"),
                Name = "Brown"
            },
        };

        private Theme selectedTheme = new Theme
        {
            Color = (SolidColorBrush)new BrushConverter().ConvertFrom("#3F51B5"),
            Name = "DeepPurple",
        };

        public Theme SelectedTheme
        {
            get => selectedTheme;
            set
            {
                selectedTheme = value;
                OnPropertyChanged();
            }
        }

        public bool IsShowSourceColumn
        {
            get => isShowSourceColumn;
            set
            {
                isShowSourceColumn = value;
                OnPropertyChanged();
            }
        }

        public bool IsShowThreadColumn
        {
            get => isShowThreadColumn;
            set
            {
                isShowThreadColumn = value;
                OnPropertyChanged();
            }
        }

        public bool IsShowTaskbarProgress
        {
            get => isShowTaskbarProgress;
            set
            {
                isShowTaskbarProgress = value;
                OnPropertyChanged();
            }
        }

        public bool IsShowErrorTimeline
        {
            get => isShowErrorTimeline;
            set
            {
                isShowErrorTimeline = value;
                OnPropertyChanged();
            }
        }

        public bool ShowMessageHighlightByReceiverColor
        {
            get => showMessageHighlightByReceiverColor;
            set
            {
                showMessageHighlightByReceiverColor = value;
                OnPropertyChanged();
            }
        }

        public bool IsSeparateIpLoggersByPort
        {
            get => isSeparateIpLoggersByPort;
            set
            {
                isSeparateIpLoggersByPort = value;
                OnPropertyChanged();
            }
        }

        public List<string> MessageFontFamilies { get; } = GetAvailableMessageFontFamilies();

        public List<double> MessageFontSizes { get; } = Enumerable.Range(10, 13).Select(x => (double)x).ToList();

        public string SelectedMessageFontFamily
        {
            get => selectedMessageFontFamily;
            set
            {
                selectedMessageFontFamily = value;
                OnPropertyChanged();
            }
        }

        public double MessageFontSize
        {
            get => messageFontSize;
            set
            {
                if (value < 10)
                    messageFontSize = 10;
                else if (value > 22)
                    messageFontSize = 22;
                else
                    messageFontSize = value;
                OnPropertyChanged();
            }
        }

        public string Version { get; set; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public List<string> EncodingList { get; set; } =
            new List<string>
            {
                "UTF-8",
                "ISO-8859-1",
                "Windows-1251",
                "Windows-1252",
                "Shift-JIS",
                "GB2312",
                "EUC-KR",
                "ISO-8859-2",
                "Windows-1250",
                "EUC-JP",
                "GBK",
                "Big5",
                "ISO-8859-15",
                "Windows-1256",
                "ISO-8859-9"
            };

        #endregion

        public SettingsViewModel()
        {
            ParseTheme();

            try
            {
                Receivers = new ObservableCollection<Receiver>(Settings.Instance.Receivers);
                IgnoredIpAdresses = new ObservableCollection<IgnoredIPAddress>(Settings.Instance.IgnoredIPs);
                IsAutoStartReadAtStartup = Settings.Instance.AutoStartInStartup;
                MinimizeToTray = Settings.Instance.MinimizeToTray;
                OnlyOneAppInstance = Settings.Instance.OnlyOneAppInstance;
                SelectedDataFormat = Settings.Instance.DataFormat;
                DisplayedDataFormat = Settings.Instance.DataFormat;
                IsEnableMaxMessageBufferSize = Settings.Instance.IsEnabledMaxMessageBufferSize;
                MaxMessageBufferSize = Settings.Instance.MaxMessageBufferSize;
                DeletedMessagesCount = Settings.Instance.DeletedMessagesCount;
                ExampleDateTime = DateTime.Now.ToString(DisplayedDataFormat);
                FontColor = FontColor.FromARGB(Settings.Instance.FontColor);
                SelectedFontColor = SelectedFontColor.FromARGB(Settings.Instance.FontColor);
                SelectedMessageFontFamily = MessageFontFamilies.Contains(Settings.Instance.MessageFontFamily)
                    ? Settings.Instance.MessageFontFamily
                    : "Consolas";
                MessageFontSize = Settings.Instance.MessageFontSize;
                IsShowSourceColumn = Settings.Instance.IsShowSourceColumn;
                IsShowThreadColumn = Settings.Instance.IsShowThreadColumn;
                IsShowTaskbarProgress = Settings.Instance.IsShowTaskbarProgress;
                IsShowErrorTimeline = Settings.Instance.IsShowErrorTimeline;
                ShowMessageHighlightByReceiverColor = Settings.Instance.ShowMessageHighlightByReceiverColor;
                IsSeparateIpLoggersByPort = Settings.Instance.IsSeparateIpLoggersByPort;
                SelectedLanguage = TranslationSource.Instance.CurrentCulture;

                var theme = Themes.FirstOrDefault(x => x.Name == Settings.Instance.CurrentTheme.Name);
                if (theme != null)
                    SelectedTheme = theme;
            }
            catch (Exception e)
            {
                logger.Warn(e, "SettingsViewModel: An error occurred while get receivers settings.");
            }
        }

        #region Commands

        private RelayCommand addReceiverCommand;
        private RelayCommand removeReceiverCommand;
        private RelayCommand addIgnoreIPCommand;
        private RelayCommand removeIgnoreIPCommand;
        private RelayCommand saveCommand;
        private RelayCommand сancelCommand;
        private RelayCommand setDefaultColorCommand;
        private RelayCommand showReleaseNotesCommand;
        private RelayCommand checkUpdatesCommand;

        public RelayCommand AddReceiverCommand => addReceiverCommand ?? (addReceiverCommand = new RelayCommand(AddReceiver));
        public RelayCommand RemoveReceiverCommand => removeReceiverCommand ?? (removeReceiverCommand = new RelayCommand(RemoveReceiver));
        public RelayCommand RemoveIgnoreIPCommand => removeIgnoreIPCommand ?? (removeIgnoreIPCommand = new RelayCommand(RemoveIgnoreIP));
        public RelayCommand AddIgnoreIPCommand => addIgnoreIPCommand ?? (addIgnoreIPCommand = new RelayCommand(AddIgnoreIP));
        public RelayCommand SaveCommand => saveCommand ?? (saveCommand = new RelayCommand(Save));
        public RelayCommand CancelCommand => сancelCommand ?? (сancelCommand = new RelayCommand(Cancel));
        public RelayCommand SetDefaultColorCommand => setDefaultColorCommand ?? (setDefaultColorCommand = new RelayCommand(SetDefaultColor));
        public RelayCommand ShowReleaseNotesCommand => showReleaseNotesCommand ?? (showReleaseNotesCommand = new RelayCommand(ShowReleaseNotes));
        public RelayCommand CheckUpdatesCommand => checkUpdatesCommand ?? (checkUpdatesCommand = new RelayCommand(CheckUpdates));

        #endregion

        /// <summary>
        /// Add a receiver to the list.
        /// </summary>
        /// <param name="obj"></param>
        private void AddReceiver(object obj)
        {
            Receivers.Add(new Receiver());
        }

        /// <summary>
        /// Remove the selected receiver.
        /// </summary>
        /// <param name="obj"></param>
        private void RemoveReceiver(object obj)
        {
            if (!Receivers.Any())
                IsConfigurationVisible = false;
            else
                Receivers.Remove(SelectedReceiver);
        }

        /// <summary>
        /// Add an IP to the ignore list.
        /// </summary>
        /// <param name="obj"></param>
        private void AddIgnoreIP(object obj)
        {
            if (IgnoredIpAdresses.All(x => !x.IP.Contains(TypedIP)))
                IgnoredIpAdresses.Add(new IgnoredIPAddress { IP = TypedIP });
        }

        /// <summary>
        /// Remove an IP from the ignore list.
        /// </summary>
        private void RemoveIgnoreIP(object obj)
        {
            IgnoredIpAdresses.Remove(SelectedIP);
        }

        private void Save(object obj)
        {
            if (Receivers.Count != Receivers.DistinctBy(x => x.Port).Count())
            {
                MessageBox.Show(Locals.SettingsSaveErrorSamePortNumber, Locals.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var window = obj as Views.SettingsWindow;
            if (window == null)
            {
                logger.Warn("Save error! Window is null!");
                return;
            }

            Settings.Instance.MinimizeToTray = MinimizeToTray;
            Settings.Instance.AutoStartInStartup = IsAutoStartReadAtStartup;
            Settings.Instance.IsShowSourceColumn = IsShowSourceColumn;
            Settings.Instance.IsShowThreadColumn = IsShowThreadColumn;
            Settings.Instance.IsShowTaskbarProgress = IsShowTaskbarProgress;
            Settings.Instance.IsShowErrorTimeline = IsShowErrorTimeline;
            Settings.Instance.ShowMessageHighlightByReceiverColor = ShowMessageHighlightByReceiverColor;
            Settings.Instance.CurrentTheme = SelectedTheme;
            Settings.Instance.DataFormat = SelectedDataFormat;
            Settings.Instance.IgnoredIPs = IgnoredIpAdresses.ToList();
            Settings.Instance.Receivers = Receivers.ToList();
            Settings.Instance.FontColor = SelectedFontColor.ToARGB();
            Settings.Instance.MessageFontFamily = SelectedMessageFontFamily;
            Settings.Instance.MessageFontSize = MessageFontSize;
            Settings.Instance.IsEnabledMaxMessageBufferSize = IsEnableMaxMessageBufferSize;
            Settings.Instance.MaxMessageBufferSize = MaxMessageBufferSize;
            Settings.Instance.DeletedMessagesCount = DeletedMessagesCount;
            Settings.Instance.OnlyOneAppInstance = OnlyOneAppInstance;
            Settings.Instance.Language = SelectedLanguage.Name;
            Settings.Instance.IsSeparateIpLoggersByPort = IsSeparateIpLoggersByPort;

            if (!string.IsNullOrEmpty(currentThemeName) && SelectedTheme.Name != currentThemeName)
                Settings.Instance.ApplyTheme();
            if(!Equals(TranslationSource.Instance.CurrentCulture, SelectedLanguage))
                Settings.Instance.ApplyLanguage(SelectedLanguage);

            window.DialogResult = Settings.Instance.Save();
        }

        /// <summary>
        /// Reset the receiver color to the default.
        /// </summary>
        /// <param name="obj"></param>
        private void SetDefaultColor(object obj)
        {
            if (obj is string param && !string.IsNullOrEmpty(param))
            {
                if (param == "Font")
                {
                    SelectedFontColor = new SolidColorBrush(Colors.White);
                }

                if (param == "Receiver")
                {
                    if (SelectedReceiver != null)
                    {
                        SelectedReceiver.Color = new SolidColorBrush(Colors.White);
                    }
                }
            }
        }

        private void Cancel(object obj)
        {
            var window = obj as Views.SettingsWindow;
            if (window == null)
            {
                logger.Warn("Cancel error! Window is null!");
                return;
            }
            window.DialogResult = false;
        }

        private void ShowReleaseNotes()
        {
            ReleaseNotesDialog releaseNotesDialog = new ReleaseNotesDialog();
            releaseNotesDialog.ShowDialog();
        }

        private void CheckUpdates()
        {
            if (UpdateManager.CheckForUpdates())
                UpdateManager.InstallNewUpdate();
            else
                MessageBox.Show(Locals.NoUpdatesFound);
        }

        private string currentThemeName = string.Empty;
        private void ParseTheme()
        {
            try
            {
                var source = Application.Current.Resources.MergedDictionaries[2].Source.ToString().Replace(".xaml", string.Empty);
                var themeName = source.Substring(source.LastIndexOf(".") + 1);
                currentThemeName = themeName;
                var theme = Themes.FirstOrDefault(x => x.Name == themeName);
                if (theme != null)
                    SelectedTheme = theme;
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while ParseTheme");
            }
        }

        private static List<string> GetAvailableMessageFontFamilies()
        {
            var families = new List<string> { "Consolas" };
            if (FontFamilyExists("Cascadia Mono"))
                families.Add("Cascadia Mono");
            families.Add("Courier New");
            families.Add("Segoe UI");
            return families;
        }

        private static bool FontFamilyExists(string familyName)
        {
            foreach (var family in Fonts.SystemFontFamilies)
            {
                if (string.Equals(family.Source, familyName, StringComparison.OrdinalIgnoreCase))
                    return true;

                foreach (var name in family.FamilyNames.Values)
                {
                    if (string.Equals(name, familyName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
