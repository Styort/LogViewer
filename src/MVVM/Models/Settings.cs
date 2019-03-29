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

        private const string SETTINGS_NAME = "settings.xml";

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
                using (FileStream fs = new FileStream($"{AppDomain.CurrentDomain.BaseDirectory}{SETTINGS_NAME}", FileMode.Create))
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
