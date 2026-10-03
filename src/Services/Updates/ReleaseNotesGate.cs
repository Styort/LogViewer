using System;

namespace LogViewer.Services.Updates
{
    /// <summary>
    /// Replaces ClickOnce <c>IsFirstRun</c>: Release Notes are shown once on the first start of a newer version,
    /// based on <c>LastRunVersion</c> in settings.xml.
    /// </summary>
    public static class ReleaseNotesGate
    {
        /// <summary>
        /// No stored version means a fresh install (or settings from before this field existed):
        /// the user did not update, so there is nothing new to show.
        /// </summary>
        public static bool ShouldShow(Version current, string lastRunVersion)
        {
            return current != null
                   && Version.TryParse(lastRunVersion, out var lastRun)
                   && current > lastRun;
        }

        /// <summary>Whether <c>LastRunVersion</c> must be rewritten (and settings saved) for this run.</summary>
        public static bool NeedsStore(Version current, string lastRunVersion)
        {
            return current != null && !string.Equals(current.ToString(), lastRunVersion, StringComparison.Ordinal);
        }
    }
}
