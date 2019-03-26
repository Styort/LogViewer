using System.ComponentModel;

namespace LogViewer.Enums
{
    public enum eImportTemplateParameters
    {
        // main
        level,
        logger,
        message,
        exception,
        newline,
        oneexception,
        var,

        // date and time
        date,
        longdate,
        shortdate,
        ticks,
        time,

        //Callsite and stacktraces
        сallsite,
        callsitelinenumber,
        stacktrace,

        //Processes, threads and assemblies
        threadid,
        threadname,
        processid,
        processinfo,
        processname,
        processtime,
        appdomain,
        hostname,
        machinename,

        other,
    }
}