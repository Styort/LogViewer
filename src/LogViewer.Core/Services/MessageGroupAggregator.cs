using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Builds <see cref="MessageGroup"/> rows from a snapshot. Intended to run off the UI thread
    /// (see MessageGroupsViewModel debounce). Does not mutate the source list.
    /// </summary>
    public static class MessageGroupAggregator
    {
        /// <summary>
        /// Groups by <see cref="MessageFingerprint.BuildKey"/>. Empty or null input → empty result, never null.
        /// Sorted by Count descending, then LastTime descending.
        /// </summary>
        public static List<MessageGroup> Build(IReadOnlyList<LogEntry> entries)
        {
            var result = new List<MessageGroup>();
            if (entries == null || entries.Count == 0)
                return result;

            var map = new Dictionary<string, MessageGroup>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                var key = MessageFingerprint.BuildKey(entry.Level, entry.Logger, entry.Message, entry.Throwable);
                if (!map.TryGetValue(key, out var group))
                {
                    group = new MessageGroup
                    {
                        Key = key,
                        Level = entry.Level,
                        Logger = entry.Logger ?? string.Empty,
                        SampleMessage = entry.Message,
                        SampleThrowable = entry.Throwable,
                        Headline = MessageFingerprint.Headline(entry.Message, entry.Throwable),
                        Count = 0,
                        FirstTime = entry.Time,
                        LastTime = entry.Time,
                        FirstIndex = i,
                        LastIndex = i
                    };
                    map[key] = group;
                }

                group.Count++;
                if (entry.Time < group.FirstTime)
                {
                    group.FirstTime = entry.Time;
                    group.FirstIndex = i;
                    // Keep the earliest original sample for display; last occurrence is for navigation.
                    group.SampleMessage = entry.Message;
                    group.SampleThrowable = entry.Throwable;
                    group.Headline = MessageFingerprint.Headline(entry.Message, entry.Throwable);
                }

                if (entry.Time >= group.LastTime)
                {
                    group.LastTime = entry.Time;
                    group.LastIndex = i;
                }
            }

            result.AddRange(map.Values);
            result.Sort(CompareGroups);
            return result;
        }

        private static int CompareGroups(MessageGroup a, MessageGroup b)
        {
            int byCount = b.Count.CompareTo(a.Count);
            if (byCount != 0)
                return byCount;
            return b.LastTime.CompareTo(a.LastTime);
        }
    }
}
