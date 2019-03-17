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
        private bool isShowIPColumn;

        #region Свойства

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
        /// Автозапуск считывания логов при старте
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
        /// Показывать иконку в трее при сворачивании приложения
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
        /// Учитывать максимальное количество сообщений
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
        /// Максимальное количество принимаемых логов
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
        /// Количество удаляемых сообщений при привышении размера буфера
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
        /// Только один экзепляр приложения
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
        /// Формат отображения даты
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
        /// Напечатанный IP
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
        /// Выбранный IP-адрес
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
        /// Текущий цвет шрифта
        /// </summary>
        public SolidColorBrush FontColor { get; set; } = new SolidColorBrush();

        /// <summary>
        /// Выбранный цвет шрифта
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
                TranslationSource.Instance.CurrentCulture = selectedLanguage;
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

        public bool IsShowIpColumn
        {
            get => isShowIPColumn;
            set
            {
                isShowIPColumn = value;
                OnPropertyChanged();
            }
        }

        public string Version { get; set; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

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
                IsShowIpColumn = Settings.Instance.IsShowIpColumn;

                var theme = Themes.FirstOrDefault(x => x.Name == Settings.Instance.CurrentTheme.Name);
                if (theme != null)
                    SelectedTheme = theme;
            }
            catch (Exception e)
            {
                logger.Warn(e, "SettingsViewModel: An error occurred while get receivers settings.");
            }
        }

        #region Команды

        private RelayCommand addReceiverCommand;
        private RelayCommand removeReceiverCommand;
        private RelayCommand addIgnoreIPCommand;
        private RelayCommand removeIgnoreIPCommand;
        private RelayCommand saveCommand;
        private RelayCommand сancelCommand;
        private RelayCommand setDefaultColorCommand;

        public RelayCommand AddReceiverCommand => addReceiverCommand ?? (addReceiverCommand = new RelayCommand(AddReceiver));
        public RelayCommand RemoveReceiverCommand => removeReceiverCommand ?? (removeReceiverCommand = new RelayCommand(RemoveReceiver));
        public RelayCommand RemoveIgnoreIPCommand => removeIgnoreIPCommand ?? (removeIgnoreIPCommand = new RelayCommand(RemoveIgnoreIP));
        public RelayCommand AddIgnoreIPCommand => addIgnoreIPCommand ?? (addIgnoreIPCommand = new RelayCommand(AddIgnoreIP));
        public RelayCommand SaveCommand => saveCommand ?? (saveCommand = new RelayCommand(Save));
        public RelayCommand CancelCommand => сancelCommand ?? (сancelCommand = new RelayCommand(Cancel));
        public RelayCommand SetDefaultColorCommand => setDefaultColorCommand ?? (setDefaultColorCommand = new RelayCommand(SetDefaultColor));

        #endregion

        /// <summary>
        /// Добавить ресивер в список
        /// </summary>
        /// <param name="obj"></param>
        private void AddReceiver(object obj)
        {
            Receivers.Add(new Receiver());
        }

        /// <summary>
        /// Удалить выбранный ресивер
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
        /// Добавить в список игнорирования IP-адрес
        /// </summary>
        /// <param name="obj"></param>
        private void AddIgnoreIP(object obj)
        {
            if (IgnoredIpAdresses.All(x => !x.IP.Contains(TypedIP)))
                IgnoredIpAdresses.Add(new IgnoredIPAddress { IP = TypedIP });
        }

        /// <summary>
        /// Удалить из списка игнорирования IP-адрес
        /// </summary>
        private void RemoveIgnoreIP(object obj)
        {
            IgnoredIpAdresses.Remove(SelectedIP);
        }

        private void Save(object obj)
        {
            if (Receivers.Count != Receivers.DistinctBy(x => x.Port).Count())
            {
                MessageBox.Show("There are the same port numbers in the sources.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Settings.Instance.IsShowIpColumn = IsShowIpColumn;
            Settings.Instance.CurrentTheme = SelectedTheme;
            Settings.Instance.DataFormat = SelectedDataFormat;
            Settings.Instance.IgnoredIPs = IgnoredIpAdresses.ToList();
            Settings.Instance.Receivers = Receivers.ToList();
            Settings.Instance.FontColor = SelectedFontColor.ToARGB();
            Settings.Instance.IsEnabledMaxMessageBufferSize = IsEnableMaxMessageBufferSize;
            Settings.Instance.MaxMessageBufferSize = MaxMessageBufferSize;
            Settings.Instance.DeletedMessagesCount = DeletedMessagesCount;
            Settings.Instance.OnlyOneAppInstance = OnlyOneAppInstance;

            if (!string.IsNullOrEmpty(currentThemeName) && SelectedTheme.Name != currentThemeName)
            {
                Settings.Instance.ApplyTheme();
            }

            window.DialogResult = Settings.Instance.Save();
        }

        /// <summary>
        /// Установить значение цвета ресивера по умолчанию
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
    }
}
