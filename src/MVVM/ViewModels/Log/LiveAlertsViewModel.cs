using System;
using LogViewer.Adapters;
using LogViewer.Core.Services;
using LogViewer.Localization;
using LogViewer.Services;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Live Error/Fatal side effects after a UI batch: optional auto-pause, sound, balloon.
    /// All three settings default off so an upgrade stays silent.
    /// </summary>
    public sealed class LiveAlertsViewModel
    {
        private readonly IAppSettings _settings;
        private readonly ReceiversViewModel _receivers;
        private readonly IAlertEffects _effects;
        private readonly LiveAlertEvaluator _evaluator = new LiveAlertEvaluator();

        public LiveAlertsViewModel(IAppSettings settings, ReceiversViewModel receivers, IAlertEffects effects)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _receivers = receivers ?? throw new ArgumentNullException(nameof(receivers));
            _effects = effects;
        }

        public void OnEntriesProcessed(object sender, LogEntriesProcessedEventArgs e)
        {
            bool autoPause = _settings.AlertAutoPauseOnError;
            bool sound = _settings.AlertSoundOnError;
            bool balloon = _settings.AlertBalloonOnError;
            if (!autoPause && !sound && !balloon)
                return;
            if (!LiveAlertEvaluator.BatchContainsAlertableEntry(e?.Entries))
                return;

            // Same Stop path as the Pause button. Skip when already paused so a storm does not re-enter.
            if (autoPause && !_receivers.StartIsEnabled)
                _receivers.Pause();

            if (_effects == null || (!sound && !balloon))
                return;
            if (!_evaluator.TryConsumeNotify(DateTime.UtcNow, LiveAlertEvaluator.DefaultNotifyThrottle))
                return;

            if (sound)
                _effects.PlayErrorSound();
            if (balloon)
                _effects.ShowErrorBalloon(Locals.LiveAlertBalloonTitle, Locals.LiveAlertBalloonText);
        }
    }
}
