using System;
using LogViewer.Core.Domain;
using LogViewer.Enums;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    /// <summary>
    /// Row in the Repeats window. First/Last are snapshot instances, not clones.
    /// </summary>
    public sealed class MessageGroupItem : BaseViewModel
    {
        public MessageGroupItem(MessageGroup group, LogMessage first, LogMessage last)
        {
            Key = group.Key;
            Level = (eLogLevel)(int)group.Level;
            Logger = group.Logger;
            Headline = group.Headline;
            SampleMessage = group.SampleMessage;
            Count = group.Count;
            FirstTime = group.FirstTime;
            LastTime = group.LastTime;
            FirstMessage = first;
            LastMessage = last;
            DisplayText = SingleLinePreview(string.IsNullOrEmpty(group.SampleMessage)
                ? group.Headline
                : group.SampleMessage);
        }

        public string Key { get; }
        public eLogLevel Level { get; }
        public string Logger { get; }
        public string Headline { get; }
        public string SampleMessage { get; }
        public int Count { get; }
        public DateTime FirstTime { get; }
        public DateTime LastTime { get; }
        public LogMessage FirstMessage { get; }
        public LogMessage LastMessage { get; }
        /// <summary>Single ListView line: newlines in Message would stretch the whole row.</summary>
        public string DisplayText { get; }

        private static string SingleLinePreview(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var newLine = text.IndexOf('\n');
            if (newLine >= 0)
                text = text.Substring(0, newLine);
            if (text.Length > 0 && text[text.Length - 1] == '\r')
                text = text.Substring(0, text.Length - 1);
            return text.Trim();
        }
    }
}
