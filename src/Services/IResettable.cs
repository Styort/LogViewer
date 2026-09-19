namespace LogViewer.Services
{
    /// <summary>
    /// Contract for <c>LogViewModel.Clean()</c>: the host does not know child fields; it only walks parts.
    /// Don't Receive and UDP sources stay outside this reset (see implementations).
    /// </summary>
    public interface IResettable
    {
        /// <summary>
        /// Restore the feature to an empty-session state. Do not stop UDP or clear Don't Receive
        /// unless the implementation chooses to — Clean clears the list, not receive settings.
        /// </summary>
        void Reset();
    }
}
