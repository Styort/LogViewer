namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Persistent highlight rule. Stored in <c>settings.xml</c> as a list; order is priority
    /// (first matching enabled rule wins). Does not filter rows — color only.
    /// </summary>
    /// <remarks>
    /// Older settings files omit the list; treat that as empty (same look as 1.2.8.x).
    /// <c>Level</c> is the enum name (<c>Error</c>, <c>Fatal</c>, …) or empty for any level.
    /// <c>ColorArgb</c> is <c>#AARRGGBB</c>. Regex flags apply only to the matching field;
    /// current SearchText is never used as a rule.
    /// </remarks>
    public class HighlightRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }

        /// <summary>Exact <see cref="LogLevel"/> name, or empty/null for any level.</summary>
        public string Level { get; set; }

        public string LoggerPattern { get; set; }
        public bool LoggerIsRegex { get; set; }

        public string MessagePattern { get; set; }
        public bool MessageIsRegex { get; set; }

        /// <summary>Row fill including alpha, e.g. <c>#4DFF0000</c>.</summary>
        public string ColorArgb { get; set; }
    }
}
