using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// User-chosen snapshot of the in-memory log buffer, bookmarks, and logger tree.
    /// Not the same as <c>filter_presets.xml</c> (task 08): presets never contain log rows.
    /// Written only via Save As — never auto-saved to Documents.
    /// </summary>
    /// <remarks>
    /// Root attribute <see cref="Version"/> is currently <c>1</c>. A missing Version is treated as v1.
    /// Version 2+ must not be loaded as v1: show an error and leave the current buffer untouched.
    /// Bookmarks use an index into <see cref="Entries"/> (save order), not a time+logger+message hash —
    /// identical lines would otherwise collide.
    /// </remarks>
    [XmlRoot("LogViewerSession")]
    public class SavedSessionDocument
    {
        public const string CurrentVersion = "1";

        /// <summary>Warn before Save when the buffer is larger than this; do not block.</summary>
        public const int LargeBufferWarningThreshold = 200000;

        [XmlAttribute("Version")]
        public string Version { get; set; }

        public string MinLevel { get; set; }
        public string SearchText { get; set; }
        public bool MatchCase { get; set; }
        public bool MatchWholeWord { get; set; }
        public bool UseRegex { get; set; }
        public bool MatchLogLevel { get; set; } = true;
        public bool IsSearchActive { get; set; }
        public bool IsTimeIntervalActive { get; set; }
        public DateTime TimeRangeFrom { get; set; }
        public DateTime TimeRangeTo { get; set; }

        /// <summary>Don't Show FullPaths (list hide). Independent from Don't Receive.</summary>
        [XmlArray("ExcludedLoggers")]
        [XmlArrayItem("Path")]
        public List<string> ExcludedLoggerFullPaths { get; set; } = new List<string>();

        /// <summary>
        /// Don't Receive FullPaths. Must be restored or live UDP after Open fills the buffer again.
        /// </summary>
        [XmlArray("DontReceiveLoggers")]
        [XmlArrayItem("Path")]
        public List<string> DontReceiveLoggerFullPaths { get; set; } = new List<string>();

        /// <summary>Show-only roots. Empty means ordinary Don't Show exclusions.</summary>
        [XmlArray("IncludedLoggers")]
        [XmlArrayItem("Path")]
        public List<string> IncludedLoggerFullPaths { get; set; } = new List<string>();

        [XmlArray("Bookmarks")]
        [XmlArrayItem("Bookmark")]
        public List<SavedSessionBookmark> Bookmarks { get; set; } = new List<SavedSessionBookmark>();

        [XmlArray("Entries")]
        [XmlArrayItem("Entry")]
        public List<SavedSessionEntry> Entries { get; set; } = new List<SavedSessionEntry>();
    }

    /// <summary>
    /// Bookmark as an index into the saved <see cref="SavedSessionDocument.Entries"/> array plus comment.
    /// Out-of-range indexes are skipped on load (corrupt file must not NRE).
    /// </summary>
    public class SavedSessionBookmark
    {
        [XmlAttribute("Index")]
        public int Index { get; set; }

        [XmlAttribute("Comment")]
        public string Comment { get; set; }
    }

    /// <summary>Serializable <see cref="LogEntry"/> fields. Dictionary properties become a list of name/value pairs.</summary>
    public class SavedSessionEntry
    {
        [XmlAttribute("Time")]
        public DateTime Time { get; set; }

        [XmlAttribute("Level")]
        public string Level { get; set; }

        [XmlAttribute("Logger")]
        public string Logger { get; set; }

        [XmlAttribute("Thread")]
        public int Thread { get; set; }

        [XmlAttribute("Address")]
        public string Address { get; set; }

        [XmlAttribute("ExecutableName")]
        public string ExecutableName { get; set; }

        [XmlAttribute("ProcessID")]
        public string ProcessID { get; set; }

        [XmlAttribute("ReceiverPort")]
        public int ReceiverPort { get; set; }

        [XmlAttribute("ReceiverTransport")]
        public string ReceiverTransport { get; set; }

        public string Message { get; set; }
        public string Throwable { get; set; }

        [XmlArray("Properties")]
        [XmlArrayItem("Property")]
        public List<SavedSessionProperty> Properties { get; set; } = new List<SavedSessionProperty>();
    }

    public class SavedSessionProperty
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Value")]
        public string Value { get; set; }
    }

    /// <summary>XML/gzip could not be parsed. The UI must not clear the current buffer.</summary>
    public class SavedSessionFormatException : Exception
    {
        public SavedSessionFormatException(string message)
            : base(message)
        {
        }

        public SavedSessionFormatException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// File Version is not 1. Do not guess a mapping — the user upgrades the app (or the file).
    /// </summary>
    public class SavedSessionUnsupportedVersionException : SavedSessionFormatException
    {
        public SavedSessionUnsupportedVersionException(string version)
            : base("Unsupported LogViewerSession Version: " + (version ?? "(null)"))
        {
            Version = version ?? string.Empty;
        }

        public string Version { get; }
    }
}
