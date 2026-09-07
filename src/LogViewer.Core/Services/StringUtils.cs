using System;
using System.Globalization;
using System.Linq;

namespace LogViewer.Core.Services
{
    internal static class StringUtils
    {
        public static string FirstCharToUpper(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) throw new ArgumentException("Input cannot be empty", nameof(input));
            return input.First().ToString().ToUpperInvariant() + input.Substring(1);
        }

        public static string ToPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var lower = text.ToLowerInvariant().Replace("_", " ");
            var info = CultureInfo.CurrentCulture.TextInfo;
            return info.ToTitleCase(lower).Replace(" ", string.Empty);
        }

        public static bool ContainsAnyOf(string line, string[] search, bool ignoreCase = false)
        {
            if (line == null || search == null) return false;
            return search.Any(x => ignoreCase
                ? line.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0
                : line.Contains(x));
        }
    }
}
