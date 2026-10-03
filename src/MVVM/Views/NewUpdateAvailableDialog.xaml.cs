using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using LogViewer.Services.Updates;
using NLog;

namespace LogViewer.MVVM.Views
{
    /// <summary>
    /// Replaces the AutoUpdater.NET WinForms form, which renders the changelog in an embedded browser and
    /// is not localized through <c>Locals</c>.
    /// </summary>
    public partial class NewUpdateAvailableDialog : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly string _changelogUrl;

        /// <summary>Closing the window without a button leaves <see cref="UpdateChoice.Dismissed"/>.</summary>
        public UpdateChoice Choice { get; private set; } = UpdateChoice.Dismissed;

        public NewUpdateAvailableDialog(UpdateCheckResult update, ICommand saveSession)
        {
            InitializeComponent();

            CurrentVersionText.Text = update?.InstalledVersion?.ToString() ?? string.Empty;
            NewVersionText.Text = update?.AvailableVersion ?? string.Empty;

            _changelogUrl = update?.ChangelogUrl;
            if (!Uri.TryCreate(_changelogUrl, UriKind.Absolute, out _))
                ChangelogBlock.Visibility = Visibility.Collapsed;

            if (saveSession != null)
                SaveSessionButton.Command = saveSession;
            else
                SaveSessionButton.Visibility = Visibility.Collapsed;
        }

        private void ChangelogLinkClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_changelogUrl) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                logger.Warn(exception, $"Could not open {_changelogUrl}");
            }
        }

        private void UpdateClick(object sender, RoutedEventArgs e) => CloseWith(UpdateChoice.Update);

        private void RemindLaterClick(object sender, RoutedEventArgs e) => CloseWith(UpdateChoice.RemindLater);

        private void SkipClick(object sender, RoutedEventArgs e) => CloseWith(UpdateChoice.Skip);

        private void CloseWith(UpdateChoice choice)
        {
            Choice = choice;
            DialogResult = true;
        }
    }
}
