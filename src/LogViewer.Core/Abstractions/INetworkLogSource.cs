namespace LogViewer.Core.Abstractions
{
    /// <summary>
    /// UDP/TCP source that binds a port before <see cref="ILogSource.Start"/>.
    /// File follow sources are not network sources.
    /// </summary>
    public interface INetworkLogSource : ILogSource
    {
        /// <summary>
        /// Bind the local port. UI shows <paramref name="errorMessage"/> when this returns false (port in use).
        /// </summary>
        bool TryInit(out string errorMessage);
    }
}
