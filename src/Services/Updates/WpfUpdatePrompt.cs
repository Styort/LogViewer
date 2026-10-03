using System.Linq;
using System.Windows;
using LogViewer.MVVM.ViewModels;
using LogViewer.MVVM.Views;

namespace LogViewer.Services.Updates
{
    internal sealed class WpfUpdatePrompt : IUpdatePrompt
    {
        public UpdateChoice Ask(UpdateCheckResult update)
        {
            var mainWindow = Application.Current?.MainWindow;
            // The buffer exists only in memory; the dialog offers the regular session save before the app closes.
            var saveSession = (mainWindow?.DataContext as LogViewModel)?.Session?.SaveCommand;

            var dialog = new NewUpdateAvailableDialog(update, saveSession);
            // A manual check runs from the modal settings window; a window hidden in the tray cannot own a visible dialog.
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? mainWindow;
            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.Topmost = true;
            }

            dialog.ShowDialog();
            return dialog.Choice;
        }
    }
}
