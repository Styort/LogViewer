using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;
using LogViewer.MVVM.ViewModels.Log;

namespace LogViewer.Services
{
    /// <summary>
    /// Import-template dialog result. The VM does not know about Window — only these fields.
    /// </summary>
    public sealed class ImportTemplateDialogResult
    {
        /// <summary>false if the user closed the dialog without OK — import must not start.</summary>
        public bool Confirmed { get; set; }

        /// <summary>Parsed template; null until the dialog is confirmed.</summary>
        public LogTemplate Template { get; set; }

        /// <summary>The user asked to overwrite the template file, not only apply it in memory.</summary>
        public bool NeedUpdateFile { get; set; }

        /// <summary>Whole file / last N MB / last N hours — passed to Core ImportRange.</summary>
        public ImportRange ImportRange { get; set; }
    }

    /// <summary>Time-interval picker result for the list filter.</summary>
    public sealed class TimeIntervalDialogResult
    {
        /// <summary>false — leave the interval unchanged (Escape / Cancel).</summary>
        public bool Confirmed { get; set; }

        /// <summary>Inclusive lower bound.</summary>
        public DateTime From { get; set; }

        /// <summary>Inclusive upper bound.</summary>
        public DateTime To { get; set; }
    }

    /// <summary>
    /// Windows and MessageBox without direct WPF calls from the ViewModel.
    /// Lets App.Tests inject fakes and avoid opening STA dialogs.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Informational MessageBox (no receivers, hints).</summary>
        void ShowInformation(string message, string caption = null);

        /// <summary>Error after which the flow usually stops (port busy, file unreadable).</summary>
        void ShowError(string message, string caption = null);

        /// <summary>Warning that does not stop the flow (partial import, etc.).</summary>
        void ShowWarning(string message, string caption = null);

        /// <summary>
        /// Bookmark comment. <paramref name="comment"/> is the previous value; false if the dialog was cancelled.
        /// </summary>
        bool TryPromptComment(string initial, out string comment);

        /// <summary>Go To Timestamp. null means the user cancelled; the selected row is unchanged.</summary>
        DateTime? SelectTimestamp(DateTime? current);

        /// <summary>Filter by time interval. <see cref="TimeIntervalDialogResult.Confirmed"/> = false — leave criteria alone.</summary>
        TimeIntervalDialogResult SelectTimeInterval(DateTime? current);

        /// <summary>
        /// Import-template dialog. <paramref name="samplePath"/> is the file used to guess the layout.
        /// </summary>
        ImportTemplateDialogResult ShowImportTemplate(string samplePath);

        /// <summary>Modal import progress. <paramref name="cancel"/> is invoked from the Cancel button.</summary>
        void ShowImportProgress(List<ImportLogFile> files, Action cancel);

        /// <summary>
        /// Separate Find All window. <paramref name="showLog"/> jumps to the main list on click.
        /// </summary>
        void ShowSearchResults(List<LogMessage> messages, string searchText, bool matchCase, bool useRegex, bool matchWholeWord, Action<LogMessage> showLog);

        /// <summary>A second call activates the already open statistics window instead of creating another.</summary>
        void ShowOrActivateLoggerStatistics(LoggerStatisticsViewModel viewModel, Action<LogMessage> showLog);

        /// <summary>Same for the bookmark list: one window per process.</summary>
        void ShowOrActivateBookmarks(BookmarkListViewModel viewModel);

        /// <summary>
        /// Modal settings. true — OK and apply; false/null — Cancel, UDP can resume as before.
        /// </summary>
        bool? ShowSettings();

        /// <summary>Yes/No question. false on No or close.</summary>
        bool Confirm(string message, string caption = null);

        /// <summary>Single-line prompt (rename). false if cancelled.</summary>
        bool TryPromptText(string title, string prompt, string initial, out string text);

        /// <summary>Filter preset manager: apply, save, rename, delete.</summary>
        void ShowFilterPresets(FilterPresetsViewModel viewModel);
    }
}
