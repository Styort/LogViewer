using System;
using System.IO;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Writes a file so that a crash, a full disk or a killed process leaves either the old or the new content,
    /// never a truncated file. Settings written in place used to lose every configured receiver that way:
    /// a half-written settings.xml fails to load and the app falls back to defaults.
    /// </summary>
    public static class AtomicFile
    {
        public static void Write(string path, Action<Stream> write)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required", nameof(path));
            if (write == null)
                throw new ArgumentNullException(nameof(write));

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Same folder as the target, so the final rename never crosses volumes.
            string temp = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    write(stream);
                    // Without this the rename can reach the disk before the data does.
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                    File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                else
                    File.Move(temp, path);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
