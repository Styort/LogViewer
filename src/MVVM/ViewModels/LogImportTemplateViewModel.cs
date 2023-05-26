using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using NLog;
using NLog.Fluent;
using NLog.LayoutRenderers;
using NLog.Layouts;
using System.Runtime.Serialization;
using NLog.LayoutRenderers.Wrappers;

namespace LogViewer.MVVM.ViewModels
{
    [Serializable]
    [DataContract]
    public class LogImportTemplateViewModel : BaseViewModel
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LogViewer", "template_import_settings.xml");

        private string importFilePath = string.Empty;
        private string templateSeparator = ";";
        private string selectedEncoding = "UTF-8";
        private bool? dialogResult;
        private bool needUpdateFile;
        private string templateString = "${longdate};${level};${callsite};${logger};${message};${exception:format=tostring}";

        #region Свойства
        [XmlIgnore]
        public Dictionary<string, List<eImportTemplateParameters>> PopularTemplates { get; set; } = new Dictionary<string, List<eImportTemplateParameters>>();

        [XmlElement(Order = 7)]
        public List<eImportTemplateParameters> SelectedPopularTemplate { get; set; }

        [XmlElement(Order = 8)]
        public ObservableCollection<LogTemplateItem> TemplateLogItems { get; set; } = new ObservableCollection<LogTemplateItem>();

        [XmlElement(Order = 1)]
        public bool IsAutomaticDetectTemplateSelected { get; set; } = true;
        [XmlElement(Order = 2)]
        public bool IsPopularTemplateSelected { get; set; }
        [XmlElement(Order = 3)]
        public bool IsUserTemplateSelected { get; set; }
        [XmlElement(Order = 4)]
        public bool IsLayoutStringTemplateSelected { get; set; }

        [XmlIgnore]
        public bool? DialogResult
        {
            get => dialogResult;
            set
            {
                dialogResult = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore]
        public LogTemplate LogTemplate { get; private set; } = new LogTemplate();

        [XmlElement(Order = 5)]
        public string TemplateString
        {
            get => templateString;
            set
            {
                templateString = value;
                OnPropertyChanged();
            }
        }

        [XmlElement(Order = 6)]
        public string TemplateSeparator
        {
            get => templateSeparator;
            set
            {
                templateSeparator = value;
                OnPropertyChanged();
            }
        }

        [XmlElement(Order = 9)]
        public bool NeedUpdateFile
        {
            get => needUpdateFile;
            set
            {
                needUpdateFile = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore]
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

        [XmlElement(Order = 10)]
        public string SelectedEncoding
        {
            get => selectedEncoding;
            set
            {
                selectedEncoding = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Конструктор

        public LogImportTemplateViewModel()
        {

        }

        public LogImportTemplateViewModel(string path)
        {
            importFilePath = path;

            PopularTemplates.Add("${date};${level};${other};${processid};${threadid};${сallsite};${logger};${message}",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.other,
                    eImportTemplateParameters.processid,
                    eImportTemplateParameters.threadid,
                    eImportTemplateParameters.сallsite,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("${date};${level};${threadid};${сallsite};${logger};${message}",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.threadid,
                    eImportTemplateParameters.сallsite,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("${date};${level};${threadid};${logger};${message}",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.threadid,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("${date};${level};${сallsite};${logger};${message}",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.сallsite,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("${date};${level};${logger};${message}",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("${longdate}|${level:uppercase=true}|${logger}|${message}",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.longdate,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            SelectedPopularTemplate = PopularTemplates.First().Value;

            // если в корневой папке лежит файл настроек - читаем оттуда
            if (!File.Exists(settingsPath) && File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}template_import_settings.xml"))
                settingsPath = $"{AppDomain.CurrentDomain.BaseDirectory}template_import_settings.xml";

            if (File.Exists(settingsPath))
            {
                var ser = new XmlSerializer(this.GetType());
                try
                {
                    using (var fs = new FileStream(settingsPath, FileMode.Open))
                    {
                        LogImportTemplateViewModel litvm = (LogImportTemplateViewModel)ser.Deserialize(fs);
                        this.IsAutomaticDetectTemplateSelected = litvm.IsAutomaticDetectTemplateSelected;
                        this.IsLayoutStringTemplateSelected = litvm.IsLayoutStringTemplateSelected;
                        this.IsPopularTemplateSelected = litvm.IsPopularTemplateSelected;
                        this.IsUserTemplateSelected = litvm.IsUserTemplateSelected;
                        this.TemplateSeparator = litvm.TemplateSeparator;
                        this.TemplateString = litvm.TemplateString;
                        this.NeedUpdateFile = litvm.NeedUpdateFile;
                        this.SelectedEncoding = litvm.SelectedEncoding;
                        this.SelectedPopularTemplate = PopularTemplates.FirstOrDefault(x => x.Value.SequenceEqual(litvm.SelectedPopularTemplate)).Value;
                        this.TemplateLogItems = new ObservableCollection<LogTemplateItem>(litvm.TemplateLogItems);

                        foreach (var templateLogItem in TemplateLogItems)
                        {
                            var ltii = templateLogItem.TemplateItems
                                .Cast<LogTemplateItemInfo>()
                                .FirstOrDefault(x => x.Parameter == templateLogItem.SelectedTemplateParameter.Parameter);
                            templateLogItem.SelectedTemplateParameter = ltii;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "An error occurred while read template settings");
                }
            }
        }

        #endregion

        #region Команды

        private RelayCommand addTemplateItemCommand;
        private RelayCommand removeTemplateItemCommand;
        private RelayCommand okCommand;
        private RelayCommand saveTemplateSettingsCommand;

        public RelayCommand AddTemplateItemCommand => addTemplateItemCommand ?? (addTemplateItemCommand = new RelayCommand(AddTemplateItem));
        public RelayCommand RemoveTemplateItemCommand => removeTemplateItemCommand ?? (removeTemplateItemCommand = new RelayCommand(RemoveTemplateItem));
        public RelayCommand OkCommand => okCommand ?? (okCommand = new RelayCommand(Confirm));
        public RelayCommand SaveTemplateSettingsCommand => saveTemplateSettingsCommand ?? (saveTemplateSettingsCommand = new RelayCommand(SaveTemplateSettingsToFile));

        #endregion

        #region Обработчики команд

        /// <summary>
        /// Подтверждаем действие
        /// </summary>
        private void Confirm()
        {
            LogTemplate.Encoding = SelectedEncoding;

            if (IsAutomaticDetectTemplateSelected)
            {
                if (TryGetLogTemplateByAutodetect()) return;
            }
            else if (IsPopularTemplateSelected)
                GetLogTemplateByPopularTemplates();
            else if (IsLayoutStringTemplateSelected)
                GetLogTemplateByParsingLayoutPattern();
            else if (IsUserTemplateSelected)
            {
                if (TryGetLogTemplateByUserGeneratedPattern()) return;
            }

            DialogResult = true;
        }

        private void AddTemplateItem()
        {
            TemplateLogItems.Add(new LogTemplateItem());
        }

        private void RemoveTemplateItem(object obj)
        {
            LogTemplateItem item = obj as LogTemplateItem;
            if (item != null)
                TemplateLogItems.Remove(item);
        }

        private void SaveTemplateSettingsToFile()
        {
            try
            {
                XmlSerializer ser = new XmlSerializer(this.GetType());
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                using (FileStream fs = new FileStream(settingsPath, FileMode.Create))
                {
                    ser.Serialize(fs, this);
                }

                MessageBox.Show(Locals.TemplateSettingsSuccessfullySaved);
            }
            catch (Exception e)
            {
                MessageBox.Show(Locals.TemplateSettingsSaveError);
                logger.Warn(e, "An error occurred while Save settings.");
            }
        }

        #endregion

        #region Работа с подбором шаблона


        private bool TryGetLogTemplateByAutodetect()
        {
            //выбираем первое сообщение 
            string firstMessage = GetFirstLogMessage();

            if (string.IsNullOrWhiteSpace(firstMessage))
            {
                MessageBox.Show(Locals.AutomaticDetectTemplateError);
                return true;
            }

            //пытаемся сопоставить
            if (!TryDetectTemplate(firstMessage))
            {
                MessageBox.Show(Locals.AutomaticDetectTemplateError);
                return true;
            }
            return false;
        }

        private bool TryGetLogTemplateByUserGeneratedPattern()
        {
            LogTemplate.Separator = TemplateSeparator;
            for (int i = 0; i < TemplateLogItems.Count; i++)
            {
                try
                {
                    LogTemplate.TemplateParameterses.Add(TemplateLogItems[i].SelectedTemplateParameter.Parameter, i);
                }
                catch (Exception exception)
                {
                    LogTemplate.TemplateParameterses.Clear();
                    MessageBox.Show(Locals.MessageTemplateErrorSameParameters);
                    return true;
                }
            }
            return false;
        }

        private void GetLogTemplateByPopularTemplates()
        {
            for (int i = 0; i < SelectedPopularTemplate.Count; i++)
            {
                LogTemplate.TemplateParameterses.Add(SelectedPopularTemplate[i], i);
            }
        }

        private void GetLogTemplateByParsingLayoutPattern()
        {
            SimpleLayout layout = new SimpleLayout(TemplateString);
            var elements = layout.Renderers;

            // минимальное количество элементов - 4 (дата, уровень лога, логгер и сообщение)
            if (elements.Count < 4)
            {
                MessageBox.Show(Locals.ParseTemplateStringError);
                return;
            }

            var separator = layout.Renderers.FirstOrDefault(x => x is LiteralLayoutRenderer);

            LogTemplate.Separator = separator != null ? separator.Render(LogEventInfo.CreateNullEvent()) : ";";

            foreach (var layoutRenderer in elements)
            {
                if (layoutRenderer is LiteralLayoutRenderer) continue;

                var currentLayoutRenderer = layoutRenderer is WrapperLayoutRendererBase rb &&
                                            rb.Inner is SimpleLayout sl ? sl.Renderers.Count > 0 ? sl.Renderers[0] : layoutRenderer : layoutRenderer;

                var i = elements.IndexOf(layoutRenderer);

                if (currentLayoutRenderer is LevelLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.level, i);
                if (currentLayoutRenderer is CallSiteLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.сallsite, i);
                if (currentLayoutRenderer is MessageLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.message, i);
                if (currentLayoutRenderer is ThreadIdLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.threadid, i);
                if (currentLayoutRenderer is ProcessIdLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.processid, i);
                if (currentLayoutRenderer is LoggerNameLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.logger, i);
                if (currentLayoutRenderer is TimeLayoutRenderer || currentLayoutRenderer is DateLayoutRenderer ||
                    currentLayoutRenderer is LongDateLayoutRenderer || currentLayoutRenderer is ShortDateLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.date, i);
                if (currentLayoutRenderer is TicksLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.ticks, i);
            }

            // в строке могут быть кастомные лейауты и SimpleLayout их не добавляет, надо распарсить самому и сверить
            //var tElements = TemplateString.Split(new[] { LogTemplate.Separator }, StringSplitOptions.None)
            //    .Where(x => !string.IsNullOrEmpty(x)).ToList();

            // индекс в массиве elements
            //int j = 0;
            //for (int i = 0; i < tElements.Count; i++)
            //{
            //    var modifyIndex = tElements[i].IndexOf(":");
            //    if (modifyIndex > 0)
            //        tElements[i] = tElements[i].Substring(0, modifyIndex);
            //    tElements[i] = tElements[i].Replace("}", "");

            //    if (j <= elements.Count)
            //    {
            //        var layoutRender = elements[j].ToString();
            //        if (!layoutRender.Contains(tElements[i]))
            //            continue;
            //    }

            //    var type = elements[j].GetType();

            //    var element = (elements[j] is WrapperLayoutRendererBase rbWrapper ? rbWrapper.Inner : elements[j] as Layout);
            //    if (elements[j] is WrapperLayoutRendererBase rb)
            //    {
            //        var innerType = rb.Inner.GetType();
            //        if(rb.Inner is SimpleLayout sl)
            //        {
            //            elements[j] = sl.Renderers[0];
            //        }
            //    }

            //    if (elements[j] is LevelLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.level, i);
            //    if (elements[j] is CallSiteLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.сallsite, i);
            //    if (elements[j] is MessageLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.message, i);
            //    if (elements[j] is ThreadIdLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.threadid, i);
            //    if (elements[j] is ProcessIdLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.processid, i);
            //    if (elements[j] is LoggerNameLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.logger, i);
            //    if (elements[j] is TimeLayoutRenderer || elements[j] is DateLayoutRenderer ||
            //        elements[j] is LongDateLayoutRenderer || elements[j] is ShortDateLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.date, i);
            //    if (elements[j] is TicksLayoutRenderer)
            //        LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.ticks, i);
            //    j++;
            //}
        }

        private string GetFirstLogMessage()
        {
            string[] logTypeArraySeparator1 =
            {
                ";Fatal;", ";Error;", ";Warn;", ";Trace;", ";Debug;", ";Info;",
                ";Fatal", ";Error", ";Warn", ";Trace", ";Debug", ";Info",
                "Fatal;", "Error;", "Warn;", "Trace;", "Debug;", "Info;"
            };

            string[] logTypeArraySeparator2 =
            {
                "|Fatal|", "|Error|", "|Warn|", "|Trace|", "|Debug|", "|Info|",
                "|Fatal", "|Error", "|Warn", "|Trace", "|Debug", "|Info",
                "Fatal|", "Error|", "Warn|", "Trace|", "Debug|", "Info|"
            };

            var sb = new StringBuilder();
            using (FileStream stream = File.Open(importFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(stream, Encoding.GetEncoding("Windows-1251")))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        //проверяем, текущая запись - это новая запись или продолжение предыдущей.
                        if (line.ContainsAnyOf(logTypeArraySeparator1) || line.ContainsAnyOf(logTypeArraySeparator2))
                        {
                            if (sb.Length != 0) break;
                            sb.Append(line);
                        }
                        else
                        {
                            sb.AppendLine(line);
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Пытаемся автоматически подобрать шаблон сообщения
        /// </summary>
        private bool TryDetectTemplate(string log)
        {
            // пробуем разные разделители
            var logSplit = log.Split(';');
            if (logSplit.Length < 4)
            {
                logSplit = log.Split('|');
                LogTemplate.Separator = "|";
            }

            eImportTemplateParameters dataParameter = eImportTemplateParameters.date;

            var dateTimeIndex = GetDateTimeIndex(logSplit, ref dataParameter);
            if (dateTimeIndex == -1) return false;

            var intIndexes = GetIntIndexes(logSplit);
            var logLevelIndex = GetLogLevelIndex(logSplit);
            if (dateTimeIndex == -1) return false;

            // оставшиеся индексы
            List<int> otherIndexes = new List<int>();

            for (int i = 0; i < logSplit.Length; i++)
            {
                if (i == dateTimeIndex)
                    continue;
                if (i == logLevelIndex)
                    continue;
                if (intIndexes.Contains(i))
                    continue;
                if (!string.IsNullOrEmpty(logSplit[i]))
                    otherIndexes.Add(i);
            }

            if (otherIndexes.Count < 2) return false;

            LogTemplate.TemplateParameterses.Add(dataParameter, dateTimeIndex);
            LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.level, logLevelIndex);
            LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.message, otherIndexes[1]);
            LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.logger, otherIndexes.First());

            if (intIndexes.Count > 0)
            {
                LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.threadid, intIndexes.Last());
            }

            return true;
        }

        /// <summary>
        /// Получаем индекс даты
        /// </summary>
        private int GetDateTimeIndex(string[] logSplit, ref eImportTemplateParameters dateTemplateParameter)
        {
            var index = -1;
            for (int i = 0; i < logSplit.Length; i++)
            {
                if (DateTime.TryParse(logSplit[i], out DateTime date))
                    return i;

                if (DateTime.TryParseExact(logSplit[i], "yy-MM-dd HH:mm:ss.ffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                    return i;


                if (long.TryParse(logSplit[i], out long tickDateTime))
                {
                    try
                    {
                        var dateFromTicks = new DateTime(tickDateTime);
                        dateTemplateParameter = eImportTemplateParameters.ticks;
                        return i;
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
            return index;
        }

        /// <summary>
        /// Получаем индексы цифровых значений
        /// </summary>
        private List<int> GetIntIndexes(string[] logSplit)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < logSplit.Length; i++)
            {
                if (Int32.TryParse(logSplit[i], out int number))
                    indexes.Add(i);
            }

            return indexes;
        }

        /// <summary>
        /// Получаем индекс уровня лога
        /// </summary>
        private int GetLogLevelIndex(string[] logSplit)
        {
            var index = -1;
            for (int i = 0; i < logSplit.Length; i++)
            {
                if (Enum.TryParse(logSplit[i], out eLogLevel level))
                    return i;
            }
            return index;
        }

        #endregion
    }
}
