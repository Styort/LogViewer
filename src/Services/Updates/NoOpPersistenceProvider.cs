using System;
using AutoUpdaterDotNET;

namespace LogViewer.Services.Updates
{
    /// <summary>
    /// Given to AutoUpdater.NET so it never filters a check by itself (and never falls back to its registry
    /// provider). Skip / remind-later are applied by <see cref="UpdateCheckHandler"/> over <see cref="JsonUpdateStateStore"/>.
    /// </summary>
    internal sealed class NoOpPersistenceProvider : IPersistenceProvider
    {
        public Version GetSkippedVersion() => null;

        public DateTime? GetRemindLater() => null;

        public void SetSkippedVersion(Version version)
        {
        }

        public void SetRemindLater(DateTime? remindLaterAt)
        {
        }
    }
}
