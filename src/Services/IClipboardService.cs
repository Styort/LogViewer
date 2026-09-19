namespace LogViewer.Services
{
    /// <summary>
    /// Clipboard. The wrapper exists because <c>Clipboard</c> needs the UI thread and is unavailable in NUnit.
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>Put text on the clipboard (Ctrl+C for rows / message). Empty string is allowed.</summary>
        void SetText(string text);
    }
}
