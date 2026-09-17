using System;

namespace LogViewer.MVVM.Models
{
    public class ErrorTimelineBucket
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int Warn { get; set; }

        public int Error { get; set; }

        public int Fatal { get; set; }

        public LogMessage FirstHit { get; set; }

        public double WarnHeight { get; set; }

        public double ErrorHeight { get; set; }

        public double FatalHeight { get; set; }

        public string ToolTip { get; set; }

        public bool HasMessages => FirstHit != null;
    }
}
