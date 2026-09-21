using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace LogViewer.Services.Wpf
{
    /// <summary>Win32 Open/Save File Dialog. OpenFolder uses Process.Start to open Explorer.</summary>
    public sealed class WpfFileDialogService : IFileDialogService
    {
        /// <inheritdoc />
        public string[] OpenFiles(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
                return dialog.FileNames;
            return null;
        }

        /// <inheritdoc />
        public string OpenFile(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
                return dialog.FileName;
            return null;
        }

        /// <inheritdoc />
        public string SaveFile(string defaultExt, string filter, string fileName)
        {
            var dialog = new SaveFileDialog
            {
                DefaultExt = defaultExt,
                Filter = filter,
                FileName = fileName
            };
            if (dialog.ShowDialog() == true)
                return dialog.FileName;
            return null;
        }

        /// <inheritdoc />
        public void OpenFolder(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                return;
            Process.Start(directoryPath);
        }
    }
}
