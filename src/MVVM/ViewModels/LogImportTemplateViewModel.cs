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

namespace LogViewer.MVVM.ViewModels
{
    public class LogImportTemplateViewModel : BaseViewModel
    {
        private readonly string[] LogTypeArray = { ";Fatal;", ";Error;", ";Warn;", ";Trace;", ";Debug;", ";Info;" };
        private string importFilePath = string.Empty;
        private bool? dialogResult;

        #region Свойства
        public Dictionary<eImportTemplateParameters, int> TemplateParameterses { get; } = new Dictionary<eImportTemplateParameters, int>();
        public Dictionary<string, List<eImportTemplateParameters>> PopularTemplates { get; set; } = new Dictionary<string, List<eImportTemplateParameters>>();
        public List<eImportTemplateParameters> SelectedPopularTemplate { get; set; }

        public ObservableCollection<LogTemplateItem> TemplateLogItems { get; set; } = new ObservableCollection<LogTemplateItem>();

        public bool IsAutomaticDetectTemplateSelected { get; set; } = true;
        public bool IsPopularTemplateSelected { get; set; }
        public bool IsUserTemplateSelected { get; set; }
        public bool? DialogResult
        {
            get => dialogResult;
            set
            {
                dialogResult = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Конструктор

        public LogImportTemplateViewModel(string path)
        {
            importFilePath = path;

            PopularTemplates.Add("DateTime;LogLevel;EventID;ProcessID;ThreadNumber;Callsite;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.EventID,
                    eImportTemplateParameters.ProcessID,
                    eImportTemplateParameters.ThreadNumber,
                    eImportTemplateParameters.Callsite,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;ThreadNumber;Callsite;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.ThreadNumber,
                    eImportTemplateParameters.Callsite,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;ThreadNumber;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.ThreadNumber,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;Callsite;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.Callsite,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
                });

            PopularTemplates.Add("DateTime;LogLevel;Logger;Message",
                new List<eImportTemplateParameters>
                {
                    eImportTemplateParameters.DateTime,
                    eImportTemplateParameters.LogLevel,
                    eImportTemplateParameters.Logger,
                    eImportTemplateParameters.Message
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
                    TemplateParameterses.Add(SelectedPopularTemplate[i], i);
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
                for (int i = 0; i < TemplateLogItems.Count; i++)
                {
                    try
                    {
                        TemplateParameterses.Add(TemplateLogItems[i].SelectedTemplateParameter, i);
                    }
                    catch (Exception exception)
                    {
                        TemplateParameterses.Clear();
                        MessageBox.Show(Locals.MessageTemplateErrorSameParameters);
                        return;
                    }
                }
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
            var logSplit = log.Split(';');
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

            TemplateParameterses.Add(eImportTemplateParameters.DateTime, dateTimeIndex);
            TemplateParameterses.Add(eImportTemplateParameters.LogLevel, logLevelIndex);
            TemplateParameterses.Add(eImportTemplateParameters.Message, otherIndexes.Last());
            TemplateParameterses.Add(eImportTemplateParameters.Logger, otherIndexes[otherIndexes.Count - 2]);

            if (intIndexes.Count > 0)
            {
                TemplateParameterses.Add(eImportTemplateParameters.ThreadNumber, intIndexes.Last());
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
