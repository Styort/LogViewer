namespace LogViewer.Services
{
        /// <summary>
        /// Open/Save file dialogs and Explorer. VMs must not reference Microsoft.Win32 — tests without STA would fail.
        /// </summary>
        public interface IFileDialogService
        {
            /// <summary>
            /// Multi-select files. null means Cancel; an empty array is unused so cancel stays distinct.
            /// </summary>
            string[] OpenFiles(string filter);

            /// <summary>Single file (session Open). null means Cancel.</summary>
            string OpenFile(string filter);

            /// <summary>Save export. null means Cancel — do not touch the file.</summary>
            string SaveFile(string defaultExt, string filter, string fileName);

            /// <summary>Open a folder in Explorer (after unpacking an archive). Missing path is a no-op.</summary>
            void OpenFolder(string directoryPath);
        }
}
