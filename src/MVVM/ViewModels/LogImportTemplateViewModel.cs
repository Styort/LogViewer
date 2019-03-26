using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using NLog;
using NLog.Fluent;
using NLog.LayoutRenderers;
using NLog.Layouts;

namespace LogViewer.MVVM.ViewModels
{
    public class LogImportTemplateViewModel : BaseViewModel
    {
        private readonly string[] LogTypeArray = { ";Fatal;", ";Error;", ";Warn;", ";Trace;", ";Debug;", ";Info;" };
        private string importFilePath = string.Empty;
        private string templateSeparator = ";";
        private bool? dialogResult;
        private string templateString = "${longdate};${level};${callsite};${logger};${message};${exception:format=tostring}";

        #region Свойства
        public Dictionary<string, List<eImportTemplateParameters>> PopularTemplates { get; set; } = new Dictionary<string, List<eImportTemplateParameters>>();
        public List<eImportTemplateParameters> SelectedPopularTemplate { get; set; }

        public ObservableCollection<LogTemplateItem> TemplateLogItems { get; set; } = new ObservableCollection<LogTemplateItem>();

        public bool IsAutomaticDetectTemplateSelected { get; set; } = true;
        public bool IsPopularTemplateSelected { get; set; }
        public bool IsUserTemplateSelected { get; set; }
        public bool IsLayoutStringTemplateSelected { get; set; }
        public bool? DialogResult
        {
            get => dialogResult;
            set
            {
                dialogResult = value;
                OnPropertyChanged();
            }
        }

        public LogTemplate LogTemplate { get; private set; } = new LogTemplate();

        public string TemplateString
        {
            get => templateString;
            set
            {
                templateString = value;
                OnPropertyChanged();
            }
        }

        public string TemplateSeparator
        {
            get => templateSeparator;
            set
            {
                templateSeparator = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Конструктор

        public LogImportTemplateViewModel(string path)
        {
            importFilePath = path;

            PopularTemplates.Add("date;level;other;processid;threadid;сallsite;logger;message",
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

            PopularTemplates.Add("date;level;threadid;сallsite;logger;message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.threadid,
                    eImportTemplateParameters.сallsite,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("date;level;threadid;logger;message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.threadid,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("date;level;сallsite;logger;message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.сallsite,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            PopularTemplates.Add("date;level;logger;message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.date,
                    eImportTemplateParameters.level,
                    eImportTemplateParameters.logger,
                    eImportTemplateParameters.message
                });

            SelectedPopularTemplate = PopularTemplates.First().Value;
        }

        #endregion

        #region Команды

        private RelayCommand addTemplateItemCommand;
        private RelayCommand removeTemplateItemCommand;
        private RelayCommand okCommand;

        public RelayCommand AddTemplateItemCommand => addTemplateItemCommand ?? (addTemplateItemCommand = new RelayCommand(AddTemplateItem));
        public RelayCommand RemoveTemplateItemCommand => removeTemplateItemCommand ?? (removeTemplateItemCommand = new RelayCommand(RemoveTemplateItem));
        public RelayCommand OkCommand => okCommand ?? (okCommand = new RelayCommand(Confirm));

        #endregion

        #region Обработчики команд

        /// <summary>
        /// Подтверждаем действие
        /// </summary>
        private void Confirm()
        {
            // выбран один из популярных типов шаблонов
            if (IsPopularTemplateSelected)
            {
                for (int i = 0; i < SelectedPopularTemplate.Count; i++)
                {
                    LogTemplate.TemplateParameterses.Add(SelectedPopularTemplate[i], i);
                }
            }

            // выбран автоматический подбор шаблона
            if (IsAutomaticDetectTemplateSelected)
            {
                //выбираем первое сообщение 
                string firstMessage = GetFirstMessage();

                if (string.IsNullOrWhiteSpace(firstMessage))
                {
                    MessageBox.Show(Locals.AutomaticDetectTemplateError);
                    return;
                }

                //пытаемся сопоставить
                if (!TryDetectTemplate(firstMessage))
                {
                    MessageBox.Show(Locals.AutomaticDetectTemplateError);
                    return;
                }
            }

            // выбрана генерация шаблона пользователем
            if (IsUserTemplateSelected)
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
                        return;
                    }
                }
            }

            if (IsLayoutStringTemplateSelected)
            {
                ParseTemplateString();
            }

            DialogResult = true;
        }

        private void AddTemplateItem(object obj)
        {
            TemplateLogItems.Add(new LogTemplateItem());
        }

        private void RemoveTemplateItem(object obj)
        {
            LogTemplateItem item = obj as LogTemplateItem;
            if (item != null)
                TemplateLogItems.Remove(item);
        }

        #endregion

        #region Работа с подбором шаблона

        private void ParseTemplateString()
        {
            SimpleLayout layout = new SimpleLayout(TemplateString);
            var elements = layout.Renderers.Where(x => !(x is LiteralLayoutRenderer)).ToList();

            // минимальное количество элементов - 4 (дата, уровень лога, логгер и сообщение)
            if (elements.Count < 4)
            {
                MessageBox.Show(Locals.ParseTemplateStringError);
                return;
            }

            var separator = layout.Renderers.FirstOrDefault(x => x is LiteralLayoutRenderer);

            LogTemplate.Separator = separator != null ? separator.Render(LogEventInfo.CreateNullEvent()) : ";";

            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i] is LevelLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.level, i);
                if (elements[i] is CallSiteLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.сallsite, i);
                if (elements[i] is MessageLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.message, i);
                if (elements[i] is ThreadIdLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.threadid, i);
                if (elements[i] is ProcessIdLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.processid, i);
                if (elements[i] is LoggerNameLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.logger, i);
                if (elements[i] is TimeLayoutRenderer || elements[i] is DateLayoutRenderer ||
                    elements[i] is LongDateLayoutRenderer || elements[i] is ShortDateLayoutRenderer)
                    LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.date, i);
            }
        }

        private string GetFirstMessage()
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
                        if (sb.Length != 0) break;
                        sb.Append(line);
                    }
                    else
                    {
                        sb.Append(Environment.NewLine);
                        sb.Append(line);
                    }
                }
            }
            return sb.ToString();
        }

        private bool TryDetectTemplate(string log)
        {
            // пробуем разные разделители
            var logSplit = log.Split(';');
            if (logSplit.Length < 4)
                logSplit = log.Split('|');
            if (logSplit.Length < 4)
                logSplit = log.Split('$');

            var dateTimeIndex = GetDateTimeIndex(logSplit);
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

            LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.date, dateTimeIndex);
            LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.level, logLevelIndex);
            LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.message, otherIndexes.Last());
            LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.logger, otherIndexes[otherIndexes.Count - 2]);

            if (intIndexes.Count > 0)
            {
                LogTemplate.TemplateParameterses.Add(eImportTemplateParameters.threadid, intIndexes.Last());
            }

            return true;
        }

        /// <summary>
        /// Получаем индекс даты
        /// </summary>
        private int GetDateTimeIndex(string[] logSplit)
        {
            var index = -1;
            for (int i = 0; i < logSplit.Length; i++)
            {
                if (DateTime.TryParse(logSplit[i], out DateTime date))
                    return i;
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
