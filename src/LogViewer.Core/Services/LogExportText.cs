using System;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Clipboard and .txt export formatting for a log row.
    /// </summary>
    /// <remarks>
    /// The parser keeps <c>throwable</c> off <c>Message</c> so search, grouping, and the stacktrace panel
    /// do not treat the stack as message text. Copy/export still concatenate them (message, then
    /// throwable) so Ctrl+C matches the old visual layout. MDC properties are not appended; they
    /// live in the details panel.
    /// </remarks>
    public static class LogExportText
    {
        public static string JoinMessageAndThrowable(string message, string throwable)
        {
            if (string.IsNullOrEmpty(throwable))
                return message ?? string.Empty;
            if (string.IsNullOrEmpty(message))
                return throwable;
            return message + Environment.NewLine + throwable;
        }
    }
}
