namespace LogViewer.Services
{
    /// <summary>
    /// Sound and tray balloon for live Error/Fatal alerts. Implementations must not block the UI thread
    /// on I/O; <c>SystemSounds.Play</c> is asynchronous.
    /// </summary>
    public interface IAlertEffects
    {
        void PlayErrorSound();
        void ShowErrorBalloon(string title, string text);
    }
}
