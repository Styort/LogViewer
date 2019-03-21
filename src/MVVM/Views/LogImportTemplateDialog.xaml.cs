using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using LogViewer.Enums;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.ViewModels;
using NLog;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Interaction logic for LogImportTemplate.xaml
    /// </summary>
    public partial class LogImportTemplateDialog : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly string[] LogTypeArray = { ";Fatal;", ";Error;", ";Warn;", ";Trace;", ";Debug;", ";Info;" };
        private string importFilePath = string.Empty;

        public Dictionary<eImportTemplateParameters, int> TemplateParameterses { get; } = new Dictionary<eImportTemplateParameters, int>();

        public LogImportTemplateDialog(string path)
        {
            InitializeComponent();
            importFilePath = path;
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            var logImportTemplateViewModel = this.DataContext as LogImportTemplateViewModel;
            if (logImportTemplateViewModel != null)
            {
                if (logImportTemplateViewModel.IsPopularTemplateSelected)
                {
                    for (int i = 0; i < logImportTemplateViewModel.SelectedPopularTemplate.Count; i++)
                    {
                        TemplateParameterses.Add(logImportTemplateViewModel.SelectedPopularTemplate[i], i);
                    }
                }

                // TODO: Автоматический подбор шаблона
                if (logImportTemplateViewModel.IsAutomaticDetectTemplateSelected)
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

                if (logImportTemplateViewModel.IsUserTemplateSelected)
                {
                    for (int i = 0; i < logImportTemplateViewModel.TemplateLogItems.Count; i++)
                    {
                        try
                        {
                            TemplateParameterses.Add(logImportTemplateViewModel.TemplateLogItems[i].SelectedTemplateParameter, i);
                        }
                        catch (Exception exception)
                        {
                            TemplateParameterses.Clear();
                            MessageBox.Show(Locals.MessageTemplateErrorSameParameters);
                            return;
                        }
                    }
                }
            }

            this.DialogResult = true;
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
                if(i == dateTimeIndex)
                    continue;
                if(i == logLevelIndex)
                    continue;
                if(intIndexes.Contains(i))
                    continue;
                if(!string.IsNullOrEmpty(logSplit[i]))
                    otherIndexes.Add(i);
            }

            if (otherIndexes.Count < 2) return false;

            TemplateParameterses.Add(eImportTemplateParameters.DateTime, dateTimeIndex);
            TemplateParameterses.Add(eImportTemplateParameters.LogLevel, logLevelIndex);
            TemplateParameterses.Add(eImportTemplateParameters.Message, otherIndexes.Last());
            TemplateParameterses.Add(eImportTemplateParameters.Logger, otherIndexes[otherIndexes.Count-2]);

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
                if(Enum.TryParse(logSplit[i], out eLogLevel level))
                    return i;
            }
            return index;
        }
    }
}
