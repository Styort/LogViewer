using System;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// One fingerprint bucket over a snapshot of the buffer or the visible list.
    /// Indices refer to that snapshot order (not Core session indices).
    /// </summary>
    public sealed class MessageGroup
    {
        public string Key { get; set; }
        public LogLevel Level { get; set; }
        public string Logger { get; set; }
        public string SampleMessage { get; set; }
        public string SampleThrowable { get; set; }
        public string Headline { get; set; }
        public int Count { get; set; }
        public DateTime FirstTime { get; set; }
        public DateTime LastTime { get; set; }
        public int FirstIndex { get; set; }
        public int LastIndex { get; set; }
    }
}
