using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Data;
using System.Xml.Serialization;
using LogViewer.Enums;
using LogViewer.Localization;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Хранит в себе выбранный параметр шаблона и список возможных параметров для показа в комбо-боксе
    /// </summary>
    [Serializable]
    [DataContract]
    public class LogTemplateItem 
    {
        public LogTemplateItemInfo SelectedTemplateParameter { get; set; }

        public LogTemplateItem()
        {
            var test = new List<LogTemplateItemInfo>
            {
                new LogTemplateItemInfo {Group = Locals.Main, Parameter = eImportTemplateParameters.level},
                new LogTemplateItemInfo {Group = Locals.Main, Parameter = eImportTemplateParameters.logger},
                new LogTemplateItemInfo {Group = Locals.Main, Parameter = eImportTemplateParameters.message},
                new LogTemplateItemInfo {Group = Locals.Main, Parameter = eImportTemplateParameters.exception},
                new LogTemplateItemInfo {Group = Locals.Main, Parameter = eImportTemplateParameters.newline},
                new LogTemplateItemInfo {Group = Locals.Main, Parameter = eImportTemplateParameters.oneexception},
                new LogTemplateItemInfo {Group = Locals.Main, Parameter = eImportTemplateParameters.var},

                new LogTemplateItemInfo {Group = Locals.DateAndTime, Parameter = eImportTemplateParameters.date},
                new LogTemplateItemInfo {Group = Locals.DateAndTime, Parameter = eImportTemplateParameters.longdate},
                new LogTemplateItemInfo {Group = Locals.DateAndTime, Parameter = eImportTemplateParameters.shortdate},
                new LogTemplateItemInfo {Group = Locals.DateAndTime, Parameter = eImportTemplateParameters.ticks},
                new LogTemplateItemInfo {Group = Locals.DateAndTime, Parameter = eImportTemplateParameters.time},

                new LogTemplateItemInfo {Group = Locals.CallsiteAndStacktraces, Parameter = eImportTemplateParameters.сallsite},
                new LogTemplateItemInfo {Group = Locals.CallsiteAndStacktraces, Parameter = eImportTemplateParameters.callsitelinenumber},
                new LogTemplateItemInfo {Group = Locals.CallsiteAndStacktraces, Parameter = eImportTemplateParameters.stacktrace},

                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.threadid},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.threadname},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.processid},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.processinfo},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.processname},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.processtime},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.appdomain},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.hostname},
                new LogTemplateItemInfo {Group = Locals.ProcessesThreadsAndAssemblies, Parameter = eImportTemplateParameters.machinename},

                new LogTemplateItemInfo {Group = Locals.Other, Parameter = eImportTemplateParameters.other},
            };

            TemplateItems = new ListCollectionView(test);
            TemplateItems.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        }

        [XmlIgnore]
        public ListCollectionView TemplateItems { get; set; }
    }
}
