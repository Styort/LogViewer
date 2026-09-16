using System;
using System.Windows.Media;
using LogViewer.Helpers;
using LogViewer.MVVM.ViewModels;

namespace LogViewer.MVVM.Models
{
    public class LoggerStatItem : BaseViewModel
    {
        public const double MaxBarWidth = 200;

        public LoggerStatItem(LoggerStat stat, int maxTotal, SolidColorBrush barBrush)
        {
            FullPath = stat.FullPath;
            Logger = stat.Logger;
            Total = stat.Total;
            Trace = stat.Trace;
            Debug = stat.Debug;
            Info = stat.Info;
            Warn = stat.Warn;
            Error = stat.Error;
            Fatal = stat.Fatal;
            LastTime = stat.LastTime;
            Percent = stat.Percent;
            LastMessage = stat.LastMessage;
            BarWidth = maxTotal > 0 ? MaxBarWidth * stat.Total / maxTotal : 0;
            IconColor = barBrush;
        }

        public string FullPath { get; }
        public string Logger { get; }
        public int Total { get; }
        public int Trace { get; }
        public int Debug { get; }
        public int Info { get; }
        public int Warn { get; }
        public int Error { get; }
        public int Fatal { get; }
        public DateTime LastTime { get; }
        public double Percent { get; }
        public double BarWidth { get; }
        public SolidColorBrush IconColor { get; }
        public LogMessage LastMessage { get; }
    }
}
