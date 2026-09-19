using System.Diagnostics;
using NLog;

namespace LogViewer.Helpers
{
    /// <summary>
    /// DEBUG-only: PresentationTraceSources data-binding warnings go to NLog.
    /// Attached from <c>App.OnStartup</c>.
    /// </summary>
    internal sealed class BindingErrorTraceListener : TraceListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override void Write(string message)
        {
        }

        public override void WriteLine(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            Logger.Warn("Binding: {0}", message);
            if (Debugger.IsAttached
                && message.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Break only on real binding errors, not on Information-level traces, so Debug
                // smoke of the main window stops on a broken Path immediately.
                Debugger.Break();
            }
        }
    }
}
