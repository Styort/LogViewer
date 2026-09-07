using System.Collections.Generic;

namespace LogViewer.Core.Domain
{
    /// <summary>
    /// Tree node for logger hierarchy. No UI. Used so UI can build WPF Node from this.
    /// </summary>
    public class LoggerTreeNode
    {
        public string Name { get; set; }
        /// <summary>
        /// Full path used for filtering (e.g. Address.ExecutableName.Logger). Empty for non-leaf nodes if not applicable.
        /// </summary>
        public string FullPath { get; set; }
        public List<LoggerTreeNode> Children { get; set; } = new List<LoggerTreeNode>();
    }
}
