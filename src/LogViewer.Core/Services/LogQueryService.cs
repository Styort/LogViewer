using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Index-based navigation over a displayed list of <see cref="LogEntry"/>.
    /// </summary>
    /// <remarks>
    /// Returns indexes, not objects: the VM maps them back to UI rows. Invalid/empty matchers
    /// yield -1 so a broken regex never jumps the selection or empties the list.
    /// Find next wraps (legacy Find Next jumped to the first hit in the whole list); Find previous
    /// and Find next by level do not wrap.
    /// </remarks>
    public sealed class LogQueryService
    {
        /// <summary>
        /// Next match after <paramref name="fromIndex"/>; wraps to the start of the list.
        /// Returns -1 when nothing matches. <paramref name="fromIndex"/> of -1 means "nothing selected".
        /// </summary>
        public int FindNext(IReadOnlyList<LogEntry> view, int fromIndex, SearchMatcher matcher, LogLevel minLevel)
        {
            if (view == null || view.Count == 0 || matcher == null || matcher.IsPatternInvalid || matcher.IsEmpty)
                return -1;

            int start = fromIndex + 1;
            if (start < 0)
                start = 0;

            for (int i = start; i < view.Count; i++)
            {
                if (MatchesSearch(view[i], matcher, minLevel))
                    return i;
            }

            // Inclusive wrap through fromIndex so a single remaining hit can be selected again.
            int wrapEnd = Math.Min(Math.Max(fromIndex, -1) + 1, view.Count);
            for (int i = 0; i < wrapEnd; i++)
            {
                if (MatchesSearch(view[i], matcher, minLevel))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Previous match before <paramref name="fromIndex"/>. Does not wrap.
        /// </summary>
        public int FindPrevious(IReadOnlyList<LogEntry> view, int fromIndex, SearchMatcher matcher, LogLevel minLevel)
        {
            if (view == null || view.Count == 0 || matcher == null || matcher.IsPatternInvalid || matcher.IsEmpty)
                return -1;
            if (fromIndex <= 0)
                return -1;

            int start = Math.Min(fromIndex, view.Count) - 1;
            for (int i = start; i >= 0; i--)
            {
                if (MatchesSearch(view[i], matcher, minLevel))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Next row with an exact level after <paramref name="fromIndex"/>. Does not wrap.
        /// </summary>
        /// <remarks>
        /// Uses <c>==</c> on the flags enum, not a min-level mask: Warn must not match Error/Fatal
        /// even though those bits are included in the Warn value.
        /// </remarks>
        public int FindNextByLevel(IReadOnlyList<LogEntry> view, int fromIndex, LogLevel exact)
        {
            if (view == null || view.Count == 0)
                return -1;

            int start = fromIndex + 1;
            if (start < 0)
                start = 0;

            for (int i = start; i < view.Count; i++)
            {
                if (view[i] != null && view[i].Level == exact)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// First row whose timestamp truncated by <paramref name="truncate"/> equals <paramref name="target"/> truncated the same way.
        /// </summary>
        /// <remarks>
        /// The Go To Timestamp dialog does not collect ticks; truncating both sides matches "same second/ms" as the user typed.
        /// </remarks>
        public int FindByTimestamp(IReadOnlyList<LogEntry> view, DateTime target, TimeSpan truncate)
        {
            if (view == null || view.Count == 0)
                return -1;

            DateTime wanted = Truncate(target, truncate);
            for (int i = 0; i < view.Count; i++)
            {
                if (view[i] == null)
                    continue;
                if (Truncate(view[i].Time, truncate) == wanted)
                    return i;
            }

            return -1;
        }

        private static bool MatchesSearch(LogEntry entry, SearchMatcher matcher, LogLevel minLevel)
        {
            if (entry == null)
                return false;
            int level = (int)entry.Level;
            // Same HasFlag rule as LogFilter (min level is a mask of allowed levels).
            if (((int)minLevel & level) != level)
                return false;
            return LogFilter.MatchesSearch(entry, matcher);
        }

        private static DateTime Truncate(DateTime dateTime, TimeSpan timeSpan)
        {
            if (timeSpan == TimeSpan.Zero)
                return dateTime;
            if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue)
                return dateTime;
            return dateTime.AddTicks(-(dateTime.Ticks % timeSpan.Ticks));
        }
    }
}
