using System;
using LogViewer.Localization;
using NLog;

namespace LogViewer.Services.Updates
{
    /// <summary>
    /// Decides what the user sees after a manifest check. The library is given a no-op persistence provider,
    /// so skip / remind-later filtering lives here: a manual check must still offer a skipped version, and
    /// the library's own remind-later timer would block later manual checks in the same session.
    /// </summary>
    public sealed class UpdateCheckHandler
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>Checks only run at startup, so "later" means the first start after this delay.</summary>
        public static readonly TimeSpan RemindLaterDelay = TimeSpan.FromDays(1);

        private readonly IUpdatePrompt _prompt;
        private readonly IDialogService _dialogs;
        private readonly IUpdateInstaller _installer;
        private readonly IUpdateStateStore _state;
        private readonly Func<DateTime> _now;

        public UpdateCheckHandler(IUpdatePrompt prompt, IDialogService dialogs, IUpdateInstaller installer,
            IUpdateStateStore state, Func<DateTime> now = null)
        {
            _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _installer = installer ?? throw new ArgumentNullException(nameof(installer));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _now = now ?? (() => DateTime.Now);
        }

        public void Handle(UpdateCheckResult result, bool manual)
        {
            if (result == null)
                return;

            if (result.Error != null)
            {
                logger.Warn(result.Error, "Update check failed");
                if (manual)
                    _dialogs.ShowError(string.Format(Locals.UpdateCheckFailed, result.Error.Message));
                return;
            }

            if (!result.IsUpdateAvailable || !Version.TryParse(result.AvailableVersion, out var available))
            {
                logger.Debug($"No update available (installed {result.InstalledVersion}, manifest {result.AvailableVersion})");
                if (manual)
                    _dialogs.ShowInformation(Locals.NoUpdatesFound);
                return;
            }

            if (!manual && IsSuppressed(available))
                return;

            var choice = _prompt.Ask(result);
            logger.Info($"Update {available} offered, user chose {choice}");

            switch (choice)
            {
                case UpdateChoice.Update:
                    if (_installer.Download(result))
                        _installer.ExitForUpdate();
                    break;
                case UpdateChoice.Skip:
                    _state.SkippedVersion = available;
                    _state.RemindLaterAt = null;
                    break;
                case UpdateChoice.RemindLater:
                    _state.RemindLaterAt = _now() + RemindLaterDelay;
                    break;
            }
        }

        private bool IsSuppressed(Version available)
        {
            var skipped = _state.SkippedVersion;
            // A newer release than the skipped one is offered again.
            if (skipped != null && available <= skipped)
            {
                logger.Debug($"Update {available} skipped by the user");
                return true;
            }

            var remindAt = _state.RemindLaterAt;
            if (remindAt != null && _now() < remindAt.Value)
            {
                logger.Debug($"Update {available} postponed until {remindAt.Value}");
                return true;
            }

            return false;
        }
    }
}
