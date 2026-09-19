using System;
using System.Collections.Generic;

namespace LogViewer.Core.State
{
    /// <summary>
    /// Set math for logger exclusions. Operates on FullPath strings only; no UI tree types.
    /// </summary>
    /// <remarks>
    /// The two HashSets are mutated in place and never replaced, so a UI checkbox pass can add/remove
    /// without invalidating other references to the same instance. Display hide
    /// (<see cref="ExcludedPaths"/>) is independent from Don't Receive
    /// (<see cref="ExcludedWithBufferPaths"/>): checking a node again lifts both, but a plain uncheck
    /// only adds to the display set.
    /// </remarks>
    public sealed class LoggerFilterState
    {
        private readonly HashSet<string> _excluded = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _excludedWithBuffer = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _includeOnly = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Display hide set. Same instance for the lifetime of this object — do not replace the HashSet.
        /// </summary>
        public IReadOnlyCollection<string> ExcludedPaths
        {
            get { return _excluded; }
        }

        /// <summary>
        /// Don't Receive set. Mutated in place like <see cref="ExcludedPaths"/>.
        /// </summary>
        public IReadOnlyCollection<string> ExcludedWithBufferPaths
        {
            get { return _excludedWithBuffer; }
        }

        /// <summary>
        /// Show-only roots remembered for loggers that are not in the tree yet.
        /// Empty means ordinary Don't Show exclusions.
        /// </summary>
        public IReadOnlyCollection<string> IncludeOnlyPaths
        {
            get { return _includeOnly; }
        }

        /// <summary>
        /// Hide a node and its descendants in the list. Does not change Don't Receive.
        /// </summary>
        public void ExcludeSubtree(string path, IEnumerable<string> childPaths)
        {
            _includeOnly.Clear();
            if (!string.IsNullOrEmpty(path))
                _excluded.Add(path);
            AddAll(_excluded, childPaths);
        }

        /// <summary>
        /// Show a node and its descendants again. Also lifts Don't Receive on that subtree.
        /// </summary>
        public void IncludeSubtree(string path, IEnumerable<string> childPaths)
        {
            _includeOnly.Clear();
            if (!string.IsNullOrEmpty(path))
            {
                _excluded.Remove(path);
                _excludedWithBuffer.Remove(path);
            }
            RemoveAll(_excluded, childPaths);
            RemoveAll(_excludedWithBuffer, childPaths);
        }

        /// <summary>
        /// Exclude every available path that is not this node or a descendant. Clears Don't Receive.
        /// Root path shows everything.
        /// </summary>
        public void ShowOnly(string path, IEnumerable<string> allAvailablePaths)
        {
            _excluded.Clear();
            _excludedWithBuffer.Clear();
            _includeOnly.Clear();
            if (allAvailablePaths == null)
                return;
            if (string.IsNullOrEmpty(path) || path == "Root")
                return;

            _includeOnly.Add(path);
            foreach (var available in allAvailablePaths)
            {
                if (string.IsNullOrEmpty(available))
                    continue;
                if (!IsInSubtree(available, path))
                    _excluded.Add(available);
            }
        }

        /// <summary>
        /// Don't Receive: drop the subtree from both the list and the session buffer.
        /// </summary>
        public void DontReceive(string path, IEnumerable<string> childPaths)
        {
            if (!string.IsNullOrEmpty(path))
            {
                _excluded.Add(path);
                _excludedWithBuffer.Add(path);
            }
            AddAll(_excluded, childPaths);
            AddAll(_excludedWithBuffer, childPaths);
        }

        /// <summary>
        /// Forget paths after a subtree is removed from the tree (Clear children).
        /// </summary>
        public void ForgetSubtree(IEnumerable<string> paths)
        {
            RemoveAll(_excluded, paths);
            RemoveAll(_excludedWithBuffer, paths);
        }

        /// <summary>
        /// Hide a single newly appeared logger (parent was unchecked). Display only.
        /// </summary>
        public void Exclude(string path)
        {
            if (!string.IsNullOrEmpty(path))
                _excluded.Add(path);
        }

        /// <summary>
        /// Also exclude from the buffer (Don't Receive inherited by a new child).
        /// </summary>
        public void ExcludeFromBuffer(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            _excluded.Add(path);
            _excludedWithBuffer.Add(path);
        }

        /// <summary>
        /// Clear display exclusions only. Don't Receive survives session Clean.
        /// </summary>
        public void ClearDisplayExclusions()
        {
            _includeOnly.Clear();
            _excluded.Clear();
        }

        /// <summary>Root checkbox / Show only Root: drop both display and buffer exclusions.</summary>
        public void ClearAll()
        {
            _excluded.Clear();
            _excludedWithBuffer.Clear();
            _includeOnly.Clear();
        }

        /// <summary>
        /// Replace Don't Show paths from a filter preset. Don't Receive stays as-is and is merged back
        /// into the display set so those loggers remain hidden.
        /// </summary>
        public void ReplaceDisplayExclusions(IEnumerable<string> paths)
        {
            _excluded.Clear();
            AddAll(_excluded, paths);
            foreach (var bufferPath in _excludedWithBuffer)
                _excluded.Add(bufferPath);
        }

        /// <summary>Remember include-only roots for loggers that appear after Apply.</summary>
        public void SetIncludeOnly(IEnumerable<string> paths)
        {
            _includeOnly.Clear();
            AddAll(_includeOnly, paths);
        }

        /// <summary>
        /// Whether a newly appeared FullPath should be Don't-Show'd by the active preset / Show only.
        /// Parent-unchecked handling in the tree still applies separately.
        /// </summary>
        public bool ShouldHideNewLogger(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return false;
            if (_includeOnly.Count > 0)
            {
                foreach (var included in _includeOnly)
                {
                    if (MatchesIncludeRoot(fullPath, included))
                        return false;
                }
                return true;
            }

            if (_excluded.Contains(fullPath))
                return true;
            foreach (var path in _excluded)
            {
                if (IsInSubtree(fullPath, path))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True if <paramref name="candidate"/> is <paramref name="subtreeRoot"/> or a descendant
        /// using '.' as the FullPath separator (not a string prefix of an unrelated logger).
        /// </summary>
        public static bool IsInSubtree(string candidate, string subtreeRoot)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(subtreeRoot))
                return false;
            if (candidate == subtreeRoot)
                return true;
            return candidate.Length > subtreeRoot.Length
                   && candidate[subtreeRoot.Length] == '.'
                   && candidate.StartsWith(subtreeRoot, StringComparison.Ordinal);
        }

        /// <summary>
        /// Logger path without the log source (imported file or IPv4). Presets store this so
        /// <c>SecurityLog</c> still matches after opening <c>2026-09-19.txt</c> from another machine.
        /// </summary>
        public static string ToPortableLoggerKey(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "Root")
                return string.Empty;

            int slash = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            if (slash >= 0)
            {
                int cut = -1;
                int cutLen = 0;
                string[] seps = { ".txt.", ".log.", ".xml.", ".csv.", ".json." };
                for (int s = 0; s < seps.Length; s++)
                {
                    int i = path.LastIndexOf(seps[s], StringComparison.OrdinalIgnoreCase);
                    if (i >= slash && i >= cut)
                    {
                        cut = i;
                        cutLen = seps[s].Length;
                    }
                }
                if (cut >= 0)
                    return path.Substring(cut + cutLen);
                return string.Empty;
            }

            int ipPrefix = Ipv4SourcePrefixLength(path);
            if (ipPrefix > 0)
                return path.Substring(ipPrefix);
            return path;
        }

        /// <summary>
        /// Whether a FullPath belongs to a preset include-root. Accepts portable keys
        /// (<c>SecurityLog</c>) and legacy values that still include the file or IP prefix.
        /// </summary>
        public static bool MatchesIncludeRoot(string candidateFullPath, string includeRoot)
        {
            if (string.IsNullOrEmpty(candidateFullPath) || string.IsNullOrEmpty(includeRoot))
                return false;
            if (IsInSubtree(candidateFullPath, includeRoot))
                return true;
            string candidateKey = ToPortableLoggerKey(candidateFullPath);
            string includeKey = ToPortableLoggerKey(includeRoot);
            if (string.IsNullOrEmpty(candidateKey) || string.IsNullOrEmpty(includeKey))
                return false;
            return IsInSubtree(candidateKey, includeKey);
        }

        /// <summary>Length of <c>a.b.c.d.</c> at the start of a UDP FullPath, otherwise 0.</summary>
        private static int Ipv4SourcePrefixLength(string path)
        {
            int dots = 0;
            int segmentStart = 0;
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (c >= '0' && c <= '9')
                    continue;
                if (c != '.')
                    return 0;
                if (i == segmentStart)
                    return 0;
                dots++;
                segmentStart = i + 1;
                if (dots == 4)
                    return i + 1;
            }
            return 0;
        }

        private static void AddAll(HashSet<string> set, IEnumerable<string> paths)
        {
            if (paths == null)
                return;
            foreach (var path in paths)
            {
                if (!string.IsNullOrEmpty(path))
                    set.Add(path);
            }
        }

        private static void RemoveAll(HashSet<string> set, IEnumerable<string> paths)
        {
            if (paths == null)
                return;
            foreach (var path in paths)
            {
                if (!string.IsNullOrEmpty(path))
                    set.Remove(path);
            }
        }
    }
}
