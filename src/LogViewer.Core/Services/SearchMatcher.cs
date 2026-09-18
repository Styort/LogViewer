using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// One compiled search used by display filtering, find next/prev, the results window, and message highlighting.
    /// </summary>
    public sealed class SearchMatcher
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

        private readonly string _literal;
        private readonly StringComparison _comparison;
        private readonly Regex _regex;
        private readonly bool _isInvalid;
        private readonly bool _isEmpty;

        private SearchMatcher(string literal, StringComparison comparison)
        {
            _literal = literal;
            _comparison = comparison;
        }

        private SearchMatcher(Regex regex)
        {
            _regex = regex;
        }

        private SearchMatcher(bool isInvalid, bool isEmpty)
        {
            _isInvalid = isInvalid;
            _isEmpty = isEmpty;
        }

        /// <summary>
        /// True when <c>useRegex</c> was set and the pattern did not compile. Callers must not treat this as "no rows match".
        /// </summary>
        public bool IsPatternInvalid
        {
            get { return _isInvalid; }
        }

        public bool IsEmpty
        {
            get { return _isEmpty; }
        }

        /// <summary>
        /// Builds a matcher for the current search box.
        /// </summary>
        /// <remarks>
        /// Whole-word uses lookaround around an escaped literal so tokens next to <c>.</c> / <c>@</c> still match
        /// (logger names). <c>\b</c> would fail when the query itself starts or ends with those characters.
        /// When regex mode is on, whole-word is ignored: the user owns the pattern.
        /// </remarks>
        public static SearchMatcher Create(string searchText, bool matchCase, bool useRegex, bool matchWholeWord)
        {
            if (string.IsNullOrEmpty(searchText))
                return new SearchMatcher(isInvalid: false, isEmpty: true);

            var options = RegexOptions.CultureInvariant;
            if (!matchCase)
                options |= RegexOptions.IgnoreCase;

            if (useRegex)
            {
                try
                {
                    return new SearchMatcher(new Regex(searchText, options, MatchTimeout));
                }
                catch (ArgumentException)
                {
                    return new SearchMatcher(isInvalid: true, isEmpty: false);
                }
            }

            if (matchWholeWord)
            {
                try
                {
                    string pattern = "(?<=^|\\W)" + Regex.Escape(searchText) + "(?=$|\\W)";
                    return new SearchMatcher(new Regex(pattern, options, MatchTimeout));
                }
                catch (ArgumentException)
                {
                    return new SearchMatcher(isInvalid: true, isEmpty: false);
                }
            }

            return new SearchMatcher(searchText, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True if any searchable field contains the pattern. Does not search <see cref="LogEntry.FullPath"/>:
        /// that value is Address + optional ExecutableName + Logger, so it would duplicate those fields.
        /// Throwable and Properties values are included so search keeps working after event-properties (task 04).
        /// </summary>
        public bool Matches(LogEntry entry)
        {
            if (entry == null || _isInvalid || _isEmpty)
                return false;

            if (FieldMatches(entry.Message)
                || FieldMatches(entry.Logger)
                || FieldMatches(entry.Address)
                || FieldMatches(entry.ExecutableName)
                || FieldMatches(entry.Thread.ToString(CultureInfo.InvariantCulture))
                || FieldMatches(entry.Throwable))
            {
                return true;
            }

            if (entry.Properties == null || entry.Properties.Count == 0)
                return false;

            foreach (var value in entry.Properties.Values)
            {
                if (FieldMatches(value))
                    return true;
            }

            return false;
        }

        public bool FieldMatches(string value)
        {
            if (string.IsNullOrEmpty(value) || _isInvalid || _isEmpty)
                return false;

            if (_regex != null)
            {
                try
                {
                    return _regex.IsMatch(value);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }

            return value.IndexOf(_literal, _comparison) >= 0;
        }

        /// <summary>
        /// Spans inside <paramref name="text"/> for in-cell highlighting. Invalid patterns yield no spans (plain text).
        /// </summary>
        public IList<SearchTextMatch> FindMatches(string text)
        {
            var result = new List<SearchTextMatch>();
            if (string.IsNullOrEmpty(text) || _isInvalid || _isEmpty)
                return result;

            if (_regex != null)
            {
                try
                {
                    foreach (Match match in _regex.Matches(text))
                    {
                        if (match == null || !match.Success || match.Length == 0)
                            continue;
                        result.Add(new SearchTextMatch(match.Index, match.Length));
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    result.Clear();
                }

                return result;
            }

            int start = 0;
            while (start <= text.Length - _literal.Length)
            {
                int index = text.IndexOf(_literal, start, _comparison);
                if (index < 0)
                    break;
                result.Add(new SearchTextMatch(index, _literal.Length));
                start = index + _literal.Length;
                if (_literal.Length == 0)
                    break;
            }

            return result;
        }
    }

    /// <summary>
    /// Inclusive highlight range in displayed message text.
    /// </summary>
    public struct SearchTextMatch
    {
        public SearchTextMatch(int index, int length)
        {
            Index = index;
            Length = length;
        }

        public int Index { get; }
        public int Length { get; }
    }
}
