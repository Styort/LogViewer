using System;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace LogViewer.Services.Wpf
{
    /// <summary>
    /// System Hand sound and a tray balloon. Balloon needs a visible NotifyIcon; that is a WinForms
    /// constraint, not a product requirement to enable MinimizeToTray — a temporary icon is created
    /// when MainWindow has not shown one yet.
    /// </summary>
    public sealed class WpfAlertEffects : IAlertEffects, IDisposable
    {
        /// <summary>
        /// Minimize-to-tray icon from MainWindow, if any. Reused so a balloon does not add a second icon.
        /// </summary>
        public static NotifyIcon SharedTrayIcon { get; set; }

        private readonly SynchronizationContext _ui;
        private readonly object _sync = new object();
        private NotifyIcon _temporaryIcon;
        private Timer _hideTimer;
        private bool _disposed;

        public WpfAlertEffects()
        {
            _ui = SynchronizationContext.Current;
        }

        public void PlayErrorSound()
        {
            // Hand.Play is fire-and-forget (system sound, no file in Documents).
            SystemSounds.Hand.Play();
        }

        public void ShowErrorBalloon(string title, string text)
        {
            if (_disposed)
                return;

            var existing = SharedTrayIcon;
            if (existing != null)
            {
                existing.ShowBalloonTip(4000, title ?? string.Empty, text ?? string.Empty, ToolTipIcon.Error);
                return;
            }

            NotifyIcon icon;
            lock (_sync)
            {
                if (_disposed)
                    return;
                if (_temporaryIcon == null)
                {
                    _temporaryIcon = new NotifyIcon
                    {
                        Icon = Properties.Resources.log1,
                        Text = "Log Viewer",
                        Visible = true
                    };
                    _temporaryIcon.BalloonTipClosed += OnTemporaryBalloonDone;
                    _temporaryIcon.BalloonTipClicked += OnTemporaryBalloonDone;
                }
                icon = _temporaryIcon;
            }

            icon.ShowBalloonTip(4000, title ?? string.Empty, text ?? string.Empty, ToolTipIcon.Error);
            ArmHideTimer();
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
                DisposeTemporaryNoLock();
            }
        }

        private void OnTemporaryBalloonDone(object sender, EventArgs e)
        {
            HideTemporaryIcon();
        }

        private void ArmHideTimer()
        {
            lock (_sync)
            {
                if (_hideTimer == null)
                    _hideTimer = new Timer(_ => HideTemporaryIcon(), null, 6000, System.Threading.Timeout.Infinite);
                else
                    _hideTimer.Change(6000, System.Threading.Timeout.Infinite);
            }
        }

        private void HideTemporaryIcon()
        {
            // NotifyIcon must be disposed on the UI thread (WinForms handle).
            if (_ui != null && SynchronizationContext.Current != _ui)
            {
                _ui.Post(_ => HideTemporaryIconCore(), null);
                return;
            }
            HideTemporaryIconCore();
        }

        private void HideTemporaryIconCore()
        {
            lock (_sync)
            {
                DisposeTemporaryNoLock();
            }
        }

        private void DisposeTemporaryNoLock()
        {
            if (_hideTimer != null)
            {
                _hideTimer.Dispose();
                _hideTimer = null;
            }
            if (_temporaryIcon == null)
                return;
            _temporaryIcon.BalloonTipClosed -= OnTemporaryBalloonDone;
            _temporaryIcon.BalloonTipClicked -= OnTemporaryBalloonDone;
            _temporaryIcon.Visible = false;
            _temporaryIcon.Dispose();
            _temporaryIcon = null;
        }
    }
}
