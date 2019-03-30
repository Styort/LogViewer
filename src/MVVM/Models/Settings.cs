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
        private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LogViewer", "settings.xml");

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
        public bool IsShowSourceColumn { get; set; } = false;
        public bool IsShowThreadColumn { get; set; } = true;
        public bool IsShowTaskbarProgress { get; set; } = true;

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
            if (File.Exists(settingsPath))
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
            else
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));

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
