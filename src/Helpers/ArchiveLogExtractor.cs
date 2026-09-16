using System;
using System.Collections.Generic;
using System.IO;
using LogViewer.Localization;
using NLog;
using SharpCompress.Archives;

namespace LogViewer.Helpers
{
    public class ExtractedArchive
    {
        public string ExtractDirectory { get; set; }
        public List<string> LogFiles { get; } = new List<string>();
    }

    public static class ArchiveLogExtractor
    {
        public static bool IsArchive(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".rar", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLogFile(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".log", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsImportableFile(string path)
        {
            return IsLogFile(path) || IsArchive(path);
        }

        public static string CreateExtractDirectory(string archivePath)
        {
            var extractDir = archivePath + ".extracted_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(extractDir);
            return extractDir;
        }

        public static ExtractedArchive ExtractLogFiles(string archivePath, string extractDirectory)
        {
            var result = new ExtractedArchive { ExtractDirectory = extractDirectory };

            using (var archive = ArchiveFactory.Open(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory)
                        continue;

                    if (string.IsNullOrEmpty(entry.Key) || !IsLogFile(entry.Key))
                        continue;

                    if (entry.IsEncrypted)
                        throw new InvalidOperationException(Locals.ArchiveIsPasswordProtected);

                    var destPath = GetSafeDestination(extractDirectory, entry.Key);
                    if (destPath == null)
                        continue;

                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    using (var entryStream = entry.OpenEntryStream())
                    using (var fileStream = File.Create(destPath))
                    {
                        entryStream.CopyTo(fileStream);
                    }

                    result.LogFiles.Add(destPath);
                }
            }

            return result;
        }

        public static void Cleanup(IEnumerable<string> extractDirectories, Logger logger)
        {
            if (extractDirectories == null)
                return;

            foreach (var dir in extractDirectories)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    continue;

                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    logger?.Warn(ex, "Failed to delete extracted archive folder {0}", dir);
                }
            }
        }

        private static string GetSafeDestination(string extractDir, string entryKey)
        {
            var normalized = entryKey.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.IsNullOrEmpty(normalized) || Path.IsPathRooted(normalized))
                return null;

            var fullExtract = Path.GetFullPath(extractDir);
            var dest = Path.GetFullPath(Path.Combine(fullExtract, normalized));
            var prefix = fullExtract.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!dest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            return dest;
        }
    }
}
