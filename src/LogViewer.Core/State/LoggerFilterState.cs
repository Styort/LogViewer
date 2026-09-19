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
        /// Hide a node and its descendants in the list. Does not change Don't Receive.
        /// </summary>
        public void ExcludeSubtree(string path, IEnumerable<string> childPaths)
        {
            if (!string.IsNullOrEmpty(path))
                _excluded.Add(path);
            AddAll(_excluded, childPaths);
        }

        /// <summary>
        /// Show a node and its descendants again. Also lifts Don't Receive on that subtree.
        /// </summary>
        public void IncludeSubtree(string path, IEnumerable<string> childPaths)
        {
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
            if (allAvailablePaths == null)
                return;
            if (string.IsNullOrEmpty(path) || path == "Root")
                return;

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
            _excluded.Clear();
        }

        /// <summary>Root checkbox / Show only Root: drop both display and buffer exclusions.</summary>
        public void ClearAll()
        {
            _excluded.Clear();
            _excludedWithBuffer.Clear();
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
