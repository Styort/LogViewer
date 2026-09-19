using System.Windows;

namespace LogViewer.Services.Wpf
{
    /// <summary>STA clipboard. ViewModels must not call System.Windows.Clipboard directly.</summary>
    public sealed class WpfClipboardService : IClipboardService
    {
        /// <inheritdoc />
        public void SetText(string text)
        {
            Clipboard.SetDataObject(text ?? string.Empty);
        }
    }
}
