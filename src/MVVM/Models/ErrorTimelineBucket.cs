using System;

namespace LogViewer.MVVM.Models
{
    public class ErrorTimelineBucket
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        /// <summary>All levels in this slice; same X axis as Warn/Error/Fatal.</summary>
        public int TotalCount { get; set; }

        public int Warn { get; set; }

        public int Error { get; set; }

        public int Fatal { get; set; }

        public LogMessage FirstHit { get; set; }

        public double WarnHeight { get; set; }

        public double ErrorHeight { get; set; }

        public double FatalHeight { get; set; }

        /// <summary>All-message density fill height; own scale so Info volume does not flatten the error stack.</summary>
        public double RateHeight { get; set; }

        public string ToolTip { get; set; }

        public bool HasMessages => FirstHit != null;
    }
}
