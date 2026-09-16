using System;
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

        public static bool ContainsAnyOf(string line, string[] search)
        {
            if (line == null || search == null) return false;
            for (int i = 0; i < search.Length; i++)
            {
                string token = search[i];
                if (token != null && token.Length > 0 && line.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }
}
