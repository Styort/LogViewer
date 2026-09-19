using System.Collections.Generic;
using System.Linq;
using LogViewer.Core.Domain;
using LogViewer.Core.State;
using LogViewer.MVVM.TreeView;

namespace LogViewer.Services
{
    /// <summary>
    /// Core <see cref="LoggerTreeNode"/> → WPF <see cref="Node"/>.
    /// IsChecked is set from display exclusions, not Don't Receive:
    /// Don't Receive is also in ExcludedPaths, but the checkbox reflects "visible in the list".
    /// </summary>
    public sealed class LoggerTreeBuilder
    {
        /// <summary>Root Node named "Root". isChecked=false hides all new children.</summary>
        public Node CreateRoot(bool isExpanded = true, bool? isChecked = true)
        {
            return new Node
            {
                Logger = "Root",
                Text = "Root",
                IsExpanded = isExpanded,
                IsChecked = isChecked,
                Source = "-"
            };
        }

        /// <summary>
        /// Full rebuild of Root children from the Core hierarchy (Don't Receive dropped branches from the session).
        /// </summary>
        public void RebuildFromCore(Node root, IReadOnlyList<LoggerTreeNode> coreRoots, LoggerFilterState filter)
        {
            if (root == null)
                return;
            var excluded = filter?.ExcludedPaths;
            root.Children.Clear();
            if (coreRoots == null)
                return;
            foreach (var r in coreRoots)
            {
                var node = BuildNodeFromCore(r, root, excluded);
                if (node == null)
                    continue;
                node.IsRoot = true;
                node.IsExpanded = true;
                node.Source = r.Name;
                node.Logger = r.Name;
                node.IsChecked = excluded == null || !excluded.Contains(r.Name);
                root.Children.Add(node);
            }
        }

        private Node BuildNodeFromCore(LoggerTreeNode core, Node parent, IReadOnlyCollection<string> excluded)
        {
            var node = new Node(parent, core.Name);
            if (parent?.Logger == "Root" && parent.Parent == null)
            {
                node.Source = core.Name;
                node.Logger = core.Name;
            }
            else if (!string.IsNullOrEmpty(core.FullPath))
                node.Logger = core.FullPath;

            node.IsChecked = string.IsNullOrEmpty(core.FullPath)
                ? parent?.IsChecked
                : (excluded == null || !excluded.Contains(core.FullPath));
            node.IsVisible = true;
            foreach (var child in core.Children ?? new List<LoggerTreeNode>())
                node.Children.Add(BuildNodeFromCore(child, node, excluded));
            return node;
        }
    }
}
