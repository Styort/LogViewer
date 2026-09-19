using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Maps a log row to a highlight color. First enabled matching rule in list order wins.
    /// </summary>
    /// <remarks>
    /// Regex instances are built in <see cref="ReplaceRules"/>, not per UDP row: compiling on
    /// every packet would stall the UI batch. Invalid patterns are dropped at compile time so
    /// a bad <c>settings.xml</c> cannot throw on the receive path (the settings dialog also
    /// rejects them on Save).
    /// </remarks>
    public sealed class HighlightEngine
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);
        private CompiledRule[] _rules = Array.Empty<CompiledRule>();

        /// <summary>Replace the active rule set. Null or empty restores the unstyled list.</summary>
        public void ReplaceRules(IEnumerable<HighlightRule> rules)
        {
            if (rules == null)
            {
                _rules = Array.Empty<CompiledRule>();
                return;
            }

            var compiled = new List<CompiledRule>();
            foreach (HighlightRule rule in rules)
            {
                if (rule == null || !rule.Enabled)
                    continue;
                if (string.IsNullOrEmpty(rule.ColorArgb))
                    continue;

                CompiledRule item = Compile(rule);
                if (item == null)
                    continue;
                compiled.Add(item);
            }

            _rules = compiled.ToArray();
        }

        /// <summary>
        /// ARGB of the first matching rule, or null when none match (receiver wash / transparent).
        /// </summary>
        public string TryMatch(LogEntry entry)
        {
            if (entry == null)
                return null;
            return TryMatch(entry.Level, entry.Logger, entry.Message);
        }

        public string TryMatch(LogLevel level, string logger, string message)
        {
            CompiledRule[] rules = _rules;
            for (int i = 0; i < rules.Length; i++)
            {
                if (Matches(rules[i], level, logger, message))
                    return rules[i].ColorArgb;
            }

            return null;
        }

        private static bool Matches(CompiledRule rule, LogLevel level, string logger, string message)
        {
            if (rule.Level.HasValue && rule.Level.Value != level)
                return false;

            if (rule.LoggerRegex != null)
            {
                if (!rule.LoggerRegex.IsMatch(logger ?? string.Empty))
                    return false;
            }
            else if (!string.IsNullOrEmpty(rule.LoggerLiteral))
            {
                if (logger == null || logger.IndexOf(rule.LoggerLiteral, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (rule.MessageRegex != null)
            {
                if (!rule.MessageRegex.IsMatch(message ?? string.Empty))
                    return false;
            }
            else if (!string.IsNullOrEmpty(rule.MessageLiteral))
            {
                if (message == null || message.IndexOf(rule.MessageLiteral, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }

        private static CompiledRule Compile(HighlightRule rule)
        {
            var compiled = new CompiledRule { ColorArgb = rule.ColorArgb };

            if (!string.IsNullOrEmpty(rule.Level))
            {
                LogLevel parsed;
                if (!Enum.TryParse(rule.Level, true, out parsed))
                    return null;
                compiled.Level = parsed;
            }

            if (!string.IsNullOrEmpty(rule.LoggerPattern))
            {
                if (rule.LoggerIsRegex)
                {
                    Regex regex = TryCreateRegex(rule.LoggerPattern);
                    if (regex == null)
                        return null;
                    compiled.LoggerRegex = regex;
                }
                else
                    compiled.LoggerLiteral = rule.LoggerPattern;
            }

            if (!string.IsNullOrEmpty(rule.MessagePattern))
            {
                if (rule.MessageIsRegex)
                {
                    Regex regex = TryCreateRegex(rule.MessagePattern);
                    if (regex == null)
                        return null;
                    compiled.MessageRegex = regex;
                }
                else
                    compiled.MessageLiteral = rule.MessagePattern;
            }

            return compiled;
        }

        private static Regex TryCreateRegex(string pattern)
        {
            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private sealed class CompiledRule
        {
            public LogLevel? Level;
            public string LoggerLiteral;
            public Regex LoggerRegex;
            public string MessageLiteral;
            public Regex MessageRegex;
            public string ColorArgb;
        }
    }
}
