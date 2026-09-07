namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// Minimal config for a UDP receiver. No UI. UI implements this from Settings.
    /// </summary>
    public interface IReceiverConfig
    {
        int Port { get; }
        string Encoding { get; }
        string Name { get; }
    }
}
