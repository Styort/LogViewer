using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LogViewer.Localization;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.MVVM.Views;

namespace LogViewer.Services.Wpf
{
    /// <summary>
    /// WPF dialogs. Statistics, repeats, and bookmark windows are process singletons:
    /// a second Show activates the existing window instead of stacking another.
    /// </summary>
    public sealed class WpfDialogService : IDialogService
    {
        private LoggerStatisticsWindow _statisticsWindow;
        private MessageGroupsWindow _messageGroupsWindow;
        private BookmarkListWindow _bookmarkListWindow;

        public void ShowInformation(string message, string caption = null)
        {
            MessageBox.Show(message, caption ?? Locals.Information, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string caption = null)
        {
            MessageBox.Show(message, caption ?? Locals.Error, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowWarning(string message, string caption = null)
        {
            MessageBox.Show(message, caption ?? Locals.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public bool TryPromptComment(string initial, out string comment)
        {
            comment = initial ?? string.Empty;
            var dialog = new BookmarkCommentDialog(comment)
            {
                Owner = Owner()
            };
            if (dialog.ShowDialog() == true)
            {
                comment = dialog.Comment;
                return true;
            }
            return false;
        }

        public DateTime? SelectTimestamp(DateTime? current)
        {
            var dialog = new SelectTimestampDialog(current);
            if (dialog.ShowDialog() == true)
                return dialog.PickedDateTime;
            return null;
        }

        public TimeIntervalDialogResult SelectTimeInterval(DateTime? current)
        {
            var dialog = new SelectTimeIntervalDialog(current);
            if (dialog.ShowDialog() == true)
            {
                return new TimeIntervalDialogResult
                {
                    Confirmed = true,
                    From = dialog.DateTimeFrom,
                    To = dialog.DateTimeTo
                };
            }
            return new TimeIntervalDialogResult { Confirmed = false };
        }

        public ImportTemplateDialogResult ShowImportTemplate(string samplePath)
        {
            var dialog = new LogImportTemplateDialog(samplePath)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.ShowDialog();
            if (!dialog.DialogResult.HasValue || !dialog.DialogResult.Value)
                return new ImportTemplateDialogResult { Confirmed = false };

            return new ImportTemplateDialogResult
            {
                Confirmed = true,
                Template = dialog.LogTemplate,
                NeedUpdateFile = dialog.NeedUpdateFile,
                ImportRange = dialog.ImportRange
            };
        }

        public IDisposable ShowImportProgress(List<ImportLogFile> files, Action cancel)
        {
            var dialog = new ImportLogsProcessDialog(files);
            dialog.ImportProcessDialogResult += (sender, result) =>
            {
                if (!result)
                    cancel?.Invoke();
            };
            dialog.Show();
            return new ImportProgressWindow(dialog);
        }

        /// <summary>
        /// Closes the modeless import window when import finishes. Close is idempotent:
        /// Cancel already closes the window before the token fires.
        /// </summary>
        private sealed class ImportProgressWindow : IDisposable
        {
            private ImportLogsProcessDialog _dialog;

            public ImportProgressWindow(ImportLogsProcessDialog dialog)
            {
                _dialog = dialog;
            }

            public void Dispose()
            {
                var dialog = _dialog;
                _dialog = null;
                if (dialog == null)
                    return;

                if (dialog.Dispatcher.CheckAccess())
                    Close(dialog);
                else
                    dialog.Dispatcher.Invoke(() => Close(dialog));
            }

            private static void Close(Window dialog)
            {
                if (dialog.IsLoaded)
                    dialog.Close();
            }
        }

        public void ShowSearchResults(List<LogMessage> messages, string searchText, bool matchCase, bool useRegex, bool matchWholeWord, Action<LogMessage> showLog)
        {
            var window = new SearchResult(messages, searchText, matchCase, useRegex, matchWholeWord);
            window.ShowLogEvent += (sender, message) => showLog?.Invoke(message);
            window.Show();
        }

        public void ShowOrActivateLoggerStatistics(LoggerStatisticsViewModel viewModel, Action<LogMessage> showLog)
        {
            if (_statisticsWindow != null)
            {
                if (_statisticsWindow.WindowState == WindowState.Minimized)
                    _statisticsWindow.WindowState = WindowState.Normal;
                _statisticsWindow.Activate();
                return;
            }

            _statisticsWindow = new LoggerStatisticsWindow(viewModel);
            _statisticsWindow.Closed += (sender, args) => _statisticsWindow = null;
            _statisticsWindow.ShowLogEvent += (sender, message) => showLog?.Invoke(message);
            _statisticsWindow.Show();
        }

        public void ShowOrActivateMessageGroups(MessageGroupsViewModel viewModel, Action<LogMessage> showLog)
        {
            if (_messageGroupsWindow != null)
            {
                if (_messageGroupsWindow.WindowState == WindowState.Minimized)
                    _messageGroupsWindow.WindowState = WindowState.Normal;
                _messageGroupsWindow.Activate();
                return;
            }

            _messageGroupsWindow = new MessageGroupsWindow(viewModel);
            _messageGroupsWindow.Closed += (sender, args) => _messageGroupsWindow = null;
            _messageGroupsWindow.ShowLogEvent += (sender, message) => showLog?.Invoke(message);
            _messageGroupsWindow.Show();
        }

        public void ShowOrActivateBookmarks(BookmarkListViewModel viewModel)
        {
            if (_bookmarkListWindow != null)
            {
                if (_bookmarkListWindow.WindowState == WindowState.Minimized)
                    _bookmarkListWindow.WindowState = WindowState.Normal;
                _bookmarkListWindow.Activate();
                return;
            }

            _bookmarkListWindow = new BookmarkListWindow(viewModel);
            _bookmarkListWindow.Closed += (sender, args) => _bookmarkListWindow = null;
            _bookmarkListWindow.Show();
        }

        public bool? ShowSettings()
        {
            var settingsDialog = new SettingsWindow();
            return settingsDialog.ShowDialog();
        }

        public bool Confirm(string message, string caption = null)
        {
            var result = MessageBox.Show(
                message,
                caption ?? Locals.Information,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public bool TryPromptText(string title, string prompt, string initial, out string text)
        {
            var dialog = new InputTextDialog(title, prompt, initial)
            {
                Owner = Owner()
            };
            if (dialog.ShowDialog() == true)
            {
                text = dialog.Text ?? string.Empty;
                return true;
            }
            text = initial ?? string.Empty;
            return false;
        }

        public void ShowFilterPresets(FilterPresetsViewModel viewModel)
        {
            var dialog = new FilterPresetsWindow(viewModel)
            {
                Owner = Owner()
            };
            dialog.ShowDialog();
        }

        private static Window Owner()
        {
            return Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive)
                   ?? Application.Current?.MainWindow;
        }
    }
}
