using System;
using System.Collections.Generic;
using System.Linq;
using LogViewer.Enums;
using LogViewer.MVVM.Models;

namespace LogViewer.Helpers
{
    public class LoggerStat
    {
        public string FullPath { get; set; }
        public string Logger { get; set; }
        public int Total { get; set; }
        public int Trace { get; set; }
        public int Debug { get; set; }
        public int Info { get; set; }
        public int Warn { get; set; }
        public int Error { get; set; }
        public int Fatal { get; set; }
        public DateTime LastTime { get; set; }
        public double Percent { get; set; }
        public LogMessage LastMessage { get; set; }
    }

    public class LoggerStatisticsResult
    {
        public List<LoggerStat> Items { get; set; } = new List<LoggerStat>();
        public int TotalMessages { get; set; }
        public int LoggerCount { get; set; }
        public int ErrorFatalCount { get; set; }
        public int Trace { get; set; }
        public int Debug { get; set; }
        public int Info { get; set; }
        public int Warn { get; set; }
        public int Error { get; set; }
        public int Fatal { get; set; }
        public DateTime? MinTime { get; set; }
        public DateTime? MaxTime { get; set; }
    }

    public static class LoggerStatistics
    {
        public static LoggerStatisticsResult Build(IEnumerable<LogMessage> messages)
        {
            var result = new LoggerStatisticsResult();
            if (messages == null)
                return result;

            var map = new Dictionary<string, LoggerStat>();
            DateTime? minTime = null;
            DateTime? maxTime = null;

            foreach (var msg in messages)
            {
                if (msg == null)
                    continue;

                result.TotalMessages++;
                var path = msg.FullPath ?? string.Empty;
                if (!map.TryGetValue(path, out var stat))
                {
                    stat = new LoggerStat
                    {
                        FullPath = path,
                        Logger = GetDisplayName(msg)
                    };
                    map[path] = stat;
                }

                stat.Total++;
                CountLevel(msg.Level, stat, result);

                if (stat.LastMessage == null || msg.Time >= stat.LastTime)
                {
                    stat.LastTime = msg.Time;
                    stat.LastMessage = msg;
                }

                if (minTime == null || msg.Time < minTime.Value)
                    minTime = msg.Time;
                if (maxTime == null || msg.Time > maxTime.Value)
                    maxTime = msg.Time;
            }

            foreach (var stat in map.Values)
            {
                stat.Percent = result.TotalMessages > 0
                    ? stat.Total * 100.0 / result.TotalMessages
                    : 0;
            }

            result.Items = map.Values
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.FullPath)
                .ToList();
            result.LoggerCount = result.Items.Count;
            result.MinTime = minTime;
            result.MaxTime = maxTime;
            return result;
        }

        private static string GetDisplayName(LogMessage msg)
        {
            var logger = msg.Logger ?? string.Empty;
            if (!string.IsNullOrEmpty(msg.ExecutableName))
                return string.IsNullOrEmpty(logger) ? msg.ExecutableName : msg.ExecutableName + "." + logger;
            return logger;
        }

        private static void CountLevel(eLogLevel level, LoggerStat stat, LoggerStatisticsResult result)
        {
            if (level == eLogLevel.Trace)
            {
                stat.Trace++;
                result.Trace++;
            }
            else if (level == eLogLevel.Debug)
            {
                stat.Debug++;
                result.Debug++;
            }
            else if (level == eLogLevel.Info)
            {
                stat.Info++;
                result.Info++;
            }
            else if (level == eLogLevel.Warn)
            {
                stat.Warn++;
                result.Warn++;
            }
            else if (level == eLogLevel.Error)
            {
                stat.Error++;
                result.Error++;
                result.ErrorFatalCount++;
            }
            else if (level == eLogLevel.Fatal)
            {
                stat.Fatal++;
                result.Fatal++;
                result.ErrorFatalCount++;
            }
        }
    }
}
