using System.ComponentModel;

namespace LogViewer.Enums
{
    public enum eImportTemplateParameters
    {
        [Description("Date Time")]
        DateTime,
        [Description("Log Level")]
        LogLevel,
        [Description("Thread Number")]
        ThreadNumber,
        [Description("Process ID")]
        ProcessID,
        [Description("Callsite")]
        Callsite,
        [Description("Event ID")]
        EventID,
        [Description("Logger")]
        Logger,
        [Description("Message")]
        Message,
        [Description("Long date")]
        longdate,
        [Description("Other")]
        Other,
    }
}