using System;
using System.Text.RegularExpressions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Stable grouping key: level + logger + normalized message + throwable headline.
    /// Replacements (GUID, ISO timestamps, hex ids, long numbers) are why 847 UDP errors
    /// with different request ids collapse to one group. The key is a string, not a hash,
    /// so it is identical across process restarts.
    /// </summary>
    public static class MessageFingerprint
    {
        /// <summary>Placeholder written in place of volatile tokens. Kept short so the rest of the text still matches.</summary>
        public const string Placeholder = "#";

        private static readonly Regex GuidWithBraces = new Regex(
            @"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}",
            RegexOptions.Compiled);

        private static readonly Regex GuidDashed = new Regex(
            @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
            RegexOptions.Compiled);

        /// <summary>ISO-8601 and common log timestamps. Applied after GUIDs so a date inside a GUID is not double-replaced.</summary>
        private static readonly Regex IsoDateTime = new Regex(
            @"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?\b",
            RegexOptions.Compiled);

        private static readonly Regex HexId = new Regex(
            @"\b(?:0x[0-9a-fA-F]{4,}|[0-9a-fA-F]{8,})\b",
            RegexOptions.Compiled);

        /// <summary>
        /// Four or more digits (request ids, ports in URLs, epoch leftovers).
        /// 1–3 digit codes such as HTTP 404 stay so different status texts do not merge.
        /// </summary>
        private static readonly Regex LongNumber = new Regex(@"\b\d{4,}\b", RegexOptions.Compiled);

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>Normalize message or a throwable first line for the key.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var s = GuidWithBraces.Replace(text, Placeholder);
            s = GuidDashed.Replace(s, Placeholder);
            s = IsoDateTime.Replace(s, Placeholder);
            s = HexId.Replace(s, Placeholder);
            s = LongNumber.Replace(s, Placeholder);
            s = Whitespace.Replace(s, " ").Trim();
            return s;
        }

        /// <summary>
        /// Throwable fingerprint is the first line only (typically <c>ExceptionType: message</c>).
        /// Stack frames include file paths and <c>:line N</c>; those would split one bug into many groups.
        /// Type names in that first line are left as-is except the same token replacements as the message.
        /// </summary>
        public static string NormalizeThrowable(string throwable)
        {
            if (string.IsNullOrEmpty(throwable))
                return string.Empty;
            return Normalize(FirstLine(throwable));
        }

        /// <summary>
        /// Grouping key. Empty throwable is not the same as a throwable whose first line normalizes
        /// to empty after replacements — we still include a distinct slot so “message only” vs
        /// “same message + exception” stay two groups.
        /// </summary>
        public static string BuildKey(LogLevel level, string logger, string message, string throwable)
        {
            bool hasThrowable = !string.IsNullOrEmpty(throwable);
            return ((int)level) + "\n"
                   + (logger ?? string.Empty) + "\n"
                   + Normalize(message) + "\n"
                   + (hasThrowable ? "T:" + NormalizeThrowable(throwable) : "T:");
        }

        /// <summary>Short label for the UI row: exception type when present, otherwise a truncated message.</summary>
        public static string Headline(string message, string throwable)
        {
            if (!string.IsNullOrEmpty(throwable))
            {
                var line = FirstLine(throwable).Trim();
                if (line.Length > 0)
                {
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                        return line.Substring(0, colon).Trim();
                    return Truncate(line, 120);
                }
            }

            return Truncate((message ?? string.Empty).Trim(), 120);
        }

        internal static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            int n = text.IndexOf('\n');
            if (n < 0)
                return text;
            var line = text.Substring(0, n);
            if (line.Length > 0 && line[line.Length - 1] == '\r')
                return line.Substring(0, line.Length - 1);
            return line;
        }

        private static string Truncate(string text, int max)
        {
            if (text.Length <= max)
                return text;
            return text.Substring(0, max - 3) + "...";
        }
    }
}
