using System;
using System.IO;
using System.Security;

namespace LogViewer.Services.Updates
{
    public static class AppDirectoryAccess
    {
        /// <summary>
        /// Probes with a real temporary file: ACLs, read-only media and Program Files cannot be judged reliably
        /// from attributes. The manifest requests asInvoker, so UAC virtualization cannot fake a successful write.
        /// </summary>
        public static bool IsWritable(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return false;

            try
            {
                var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is SecurityException
                                      || e is ArgumentException || e is NotSupportedException)
            {
                return false;
            }
        }
    }
}
