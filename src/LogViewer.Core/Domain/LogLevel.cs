using System;

namespace LogViewer.Core.Domain
{
    [Flags]
    public enum LogLevel
    {
        Trace = 1 | Debug | Info | Warn | Error | Fatal,
        Debug = 2 | Info | Warn | Error | Fatal,
        Info = 4 | Warn | Error | Fatal,
        Warn = 8 | Error | Fatal,
        Error = 16 | Fatal,
        Fatal = 32,
    }
}
