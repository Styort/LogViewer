using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Web;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using LogViewer.Localization;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Синглтон с настройками
    /// </summary>
    [Serializable]
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [AspNetHostingPermission(SecurityAction.LinkDemand, Level = AspNetHostingPermissionLevel.Minimal)]
    public sealed class Settings : ISerializable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly Settings instance = new Settings();
        private string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LogViewer", "settings.xml");

        public bool AutoStartInStartup { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool OnlyOneAppInstance { get; set; } = false;
        public bool IsEnabledMaxMessageBufferSize { get; set; } = false;
        public int MaxMessageBufferSize { get; set; } = 1000000;
        public int DeletedMessagesCount { get; set; } = 100000;
        public string DataFormat { get; set; } = "dd/MM/yyyy HH:mm:ss.fff";
        public string FontColor { get; set; } = "#FFFFFFFF";
        public string Language { get; set; } = "en";
        public Theme CurrentTheme { get; set; } = new Theme
        {
            Name = "Indigo",
            Color = (SolidColorBrush)new BrushConverter().ConvertFrom("#3F51B5"),
        };
        public List<Receiver> Receivers { get; set; } = new List<Receiver>();
        public List<IgnoredIPAddress> IgnoredIPs { get; set; } = new List<IgnoredIPAddress>();

        /// <summary>
        /// Показывать ли колонку с источником 
        /// </summary>
        public bool IsShowSourceColumn { get; set; } = false;
        
        /// <summary>
        /// Показывать ли колонку с номером потока
        /// </summary>
        public bool IsShowThreadColumn { get; set; } = true;

        /// <summary>
        /// Показывать ли прогресс в таскбаре
        /// </summary>
        public bool IsShowTaskbarProgress { get; set; } = true;

        /// <summary>
        /// Подсвечивать сообщение тем же цветом, что и цвет ресивера, но только с прозрачностью
        /// </summary>
        public bool ShowMessageHighlightByReceiverColor { get; set; } = false;

        /// <summary>
        /// Разделять IP логгеры по портам
        /// </summary>
        public bool IsSeparateIpLoggersByPort { get; set; } = false;


        private Settings()
        {
        }

        public static Settings Instance => instance;

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.SetType(typeof(SingletonSerializationHelper));
        }

        /// <summary>
        /// Применить тему
        /// </summary>
        public void ApplyTheme()
        {
            try
            {
                Application.Current.Resources.MergedDictionaries[2] = new ResourceDictionary { Source = CurrentTheme.ThemeUri };
            }
            catch (Exception e)
            {
                logger.Warn(e, $"An error occurred while ApplyTheme. SelectedTheme - {CurrentTheme.Name}");
            }
        }

        /// <summary>
        /// Применить язык по названию
        /// </summary>
        public void ApplyLanguage(string lang)
        {
            TranslationSource.Instance.CurrentCulture = new CultureInfo(lang);
        }

        /// <summary>
        /// Применить язык по культуре
        /// </summary>
        public void ApplyLanguage(CultureInfo culture)
        {
            TranslationSource.Instance.CurrentCulture = culture;
        }

        /// <summary>
        /// Сохраняет
        /// </summary>
        /// <returns></returns>
        public bool Save()
        {
            try
            {
                XmlSerializer ser = new XmlSerializer(Instance.GetType());
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                using (FileStream fs = new FileStream(settingsPath, FileMode.Create))
                {
                    ser.Serialize(fs, Instance);
                }
                return true;
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while Save settings.");
                return false;
            }
        }

        public void Load()
        {
            var ser = new XmlSerializer(Settings.Instance.GetType());

            bool settingsFileExists = File.Exists(settingsPath);

            // если в корневой папке лежит файл настроек - читаем оттуда
            if (!settingsFileExists && File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}settings.xml"))
            {
                settingsPath = $"{AppDomain.CurrentDomain.BaseDirectory}settings.xml";
                settingsFileExists = true;
            }
            
            if (settingsFileExists)
            {
                try
                {
                    using (var fs = new FileStream(settingsPath, FileMode.Open))
                    {
                        Settings settings = (Settings)ser.Deserialize(fs);
                        Instance.AutoStartInStartup = settings.AutoStartInStartup;
                        Instance.MinimizeToTray = settings.MinimizeToTray;
                        Instance.CurrentTheme = settings.CurrentTheme;
                        Instance.DataFormat = settings.DataFormat;
                        Instance.IgnoredIPs = settings.IgnoredIPs;
                        Instance.Receivers = settings.Receivers;
                        Instance.FontColor = settings.FontColor;
                        Instance.OnlyOneAppInstance = settings.OnlyOneAppInstance;
                        Instance.IsEnabledMaxMessageBufferSize = settings.IsEnabledMaxMessageBufferSize;
                        Instance.MaxMessageBufferSize = settings.MaxMessageBufferSize;
                        Instance.DeletedMessagesCount = settings.DeletedMessagesCount;
                        Instance.IsShowSourceColumn = settings.IsShowSourceColumn;
                        Instance.IsShowThreadColumn = settings.IsShowThreadColumn;
                        Instance.IsShowTaskbarProgress = settings.IsShowTaskbarProgress;
                        Instance.ShowMessageHighlightByReceiverColor = settings.ShowMessageHighlightByReceiverColor;
                        Instance.IsSeparateIpLoggersByPort = settings.IsSeparateIpLoggersByPort;
                        Instance.Language = settings.Language;
                    }

                    Instance.ApplyTheme();
                    Instance.ApplyLanguage(Instance.Language);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "An error occurred while read and apply settings.");
                }
            }
        }
    }

    [Serializable]
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
    [AspNetHostingPermission(SecurityAction.LinkDemand, Level = AspNetHostingPermissionLevel.Minimal)]
    internal sealed class SingletonSerializationHelper : IObjectReference
    {
        public object GetRealObject(StreamingContext context)
        {
            return Settings.Instance;
        }
    }
}
