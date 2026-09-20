using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;

namespace LogViewer.App.Tests
{
    /// <summary>
    /// STA fakes for UI abstractions. App.Tests must not open real windows or Settings.Instance.
    /// </summary>
    internal sealed class FakeAppSettings : IAppSettings
    {
        public bool AutoStartInStartup => false;
        public bool AlertAutoPauseOnError => false;
        public bool AlertSoundOnError => false;
        public bool AlertBalloonOnError => false;
        public bool IsEnabledMaxMessageBufferSize => false;
        public int MaxMessageBufferSize => 1000;
        public int DeletedMessagesCount => 100;
        public string DataFormat => "dd/MM/yyyy HH:mm:ss.fff";
        public string FontColor => "#FFFFFFFF";
        public string MessageFontFamily => "Consolas";
        public double MessageFontSize => 14;
        public Theme CurrentTheme { get; } = new Theme();
        public List<Receiver> Receivers { get; } = new List<Receiver>();
        public List<IgnoredIPAddress> IgnoredIPs { get; } = new List<IgnoredIPAddress>();
        public bool IsShowSourceColumn => false;
        public bool IsShowThreadColumn => true;
        public bool IsShowTaskbarProgress => false;
        public bool IsShowErrorTimeline => true;
        public bool ShowMessageHighlightByReceiverColor => false;
        public List<HighlightRuleItem> HighlightRules { get; } = new List<HighlightRuleItem>();
        public bool IsSeparateIpLoggersByPort => false;
        public bool Save() => true;
    }

    internal sealed class FakeDialogs : IDialogService
    {
        public string CommentToReturn = "note";
        public bool PromptCommentResult = true;
        public DateTime? TimestampToReturn;
        public TimeIntervalDialogResult IntervalToReturn = new TimeIntervalDialogResult { Confirmed = false };

        public void ShowInformation(string message, string caption = null) { }
        public void ShowError(string message, string caption = null) { }
        public void ShowWarning(string message, string caption = null) { }
        public bool TryPromptComment(string initial, out string comment)
        {
            comment = CommentToReturn;
            return PromptCommentResult;
        }
        public DateTime? SelectTimestamp(DateTime? current) => TimestampToReturn;
        public TimeIntervalDialogResult SelectTimeInterval(DateTime? current) => IntervalToReturn;
        public ImportTemplateDialogResult ShowImportTemplate(string samplePath) => new ImportTemplateDialogResult { Confirmed = false };
        public void ShowImportProgress(List<ImportLogFile> files, Action cancel) { }
        public void ShowSearchResults(List<LogMessage> messages, string searchText, bool matchCase, bool useRegex, bool matchWholeWord, Action<LogMessage> showLog) { }
        public void ShowOrActivateLoggerStatistics(LoggerStatisticsViewModel viewModel, Action<LogMessage> showLog) { }
        public void ShowOrActivateMessageGroups(MessageGroupsViewModel viewModel, Action<LogMessage> showLog) { }
        public void ShowOrActivateBookmarks(BookmarkListViewModel viewModel) { }
        public bool? ShowSettings() => false;
        public bool Confirm(string message, string caption = null) => ConfirmResult;
        public bool TryPromptText(string title, string prompt, string initial, out string text)
        {
            text = PromptTextToReturn;
            return PromptTextResult;
        }
        public void ShowFilterPresets(FilterPresetsViewModel viewModel) { }

        public bool ConfirmResult = true;
        public string PromptTextToReturn = "Payments errors";
        public bool PromptTextResult = true;
    }

    internal sealed class FakeUiTimer : IUiTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }
        public event EventHandler Tick;
        public void Start() => IsEnabled = true;
        public void Stop() => IsEnabled = false;
        public void Dispose() => Stop();
        public void Fire()
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }

    internal sealed class FakeFiles : IFileDialogService
    {
        public string[] OpenFiles(string filter) => null;
        public string SaveFile(string defaultExt, string filter, string fileName) => null;
        public void OpenFolder(string directoryPath) { }
    }

    internal sealed class FakeClipboard : IClipboardService
    {
        public string LastText;
        public void SetText(string text) => LastText = text;
    }
}
