using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Named display-filter snapshot. Stored in <c>filter_presets.xml</c>, not in <c>settings.xml</c>.
    /// Does not include the log buffer, bookmarks, or Don't Receive — those belong to a saved session (task 13).
    /// </summary>
    /// <remarks>
    /// XML root version is on <see cref="FilterPresetDocument"/>. Missing fields in an older file
    /// use the property defaults (empty search, min level Trace, no interval).
    /// Don't Receive is omitted on purpose: a preset means "hide in the list", not "stop writing to RAM".
    /// </remarks>
    public class FilterPreset
    {
        public string Name { get; set; }

        /// <summary><see cref="LogLevel"/> enum name, e.g. <c>Error</c>. Empty/unknown → Trace.</summary>
        public string MinLevel { get; set; }

        public string SearchText { get; set; }
        public bool MatchCase { get; set; }
        public bool MatchWholeWord { get; set; }
        public bool UseRegex { get; set; }

        /// <summary>Null in old XML means default true (same as the toolbar toggle).</summary>
        public bool? MatchLogLevel { get; set; }

        public bool IsSearchActive { get; set; }

        /// <summary>
        /// When true, Apply leaves the current time interval alone (on-call: keep "now" while swapping search/level).
        /// </summary>
        public bool LeaveTimeIntervalUnchanged { get; set; }

        public bool IsTimeIntervalActive { get; set; }

        /// <summary>
        /// Sliding window: last <see cref="RelativeMinutes"/> minutes.
        /// <see cref="TimeRangeFrom"/> / <see cref="TimeRangeTo"/> are recomputed on every Apply from "now".
        /// Absolute From/To are used only when this flag is false.
        /// </summary>
        public bool IsRelativeTimeInterval { get; set; }

        public int RelativeMinutes { get; set; }

        public DateTime TimeRangeFrom { get; set; }
        public DateTime TimeRangeTo { get; set; }

        /// <summary>
        /// Legacy Don't-Show dump. New saves leave this empty: the tree is restored from
        /// <see cref="IncludedLoggerFullPaths"/>. Kept so older files still Apply.
        /// </summary>
        [XmlArray("ExcludedLoggerFullPaths")]
        [XmlArrayItem("Path")]
        public List<string> ExcludedLoggerFullPaths { get; set; }

        /// <summary>
        /// Checked logger names without the log source (file path or IP), e.g. <c>SecurityLog</c>.
        /// Descendants stay visible. Apply matches the same names on another file or host.
        /// Empty together with an empty excluded list means "show every logger".
        /// Older files may still contain FullPath values; Apply strips the source prefix.
        /// </summary>
        [XmlArray("IncludedLoggerFullPaths")]
        [XmlArrayItem("Path")]
        public List<string> IncludedLoggerFullPaths { get; set; }
    }

    /// <summary>
    /// Root of <c>%Documents%\LogViewer\filter_presets.xml</c> (same folder as <c>settings.xml</c> / <c>template_import_settings.xml</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="Version"/> is currently <c>1</c>. A file with no Version attribute is treated as v1.
    /// Deleting a preset rewrites this file only — it must not touch <c>settings.xml</c>.
    /// </remarks>
    [XmlRoot("FilterPresets")]
    public class FilterPresetDocument
    {
        public const string CurrentVersion = "1";

        [XmlAttribute("Version")]
        public string Version { get; set; }

        [XmlElement("Preset")]
        public List<FilterPreset> Presets { get; set; } = new List<FilterPreset>();
    }
}
