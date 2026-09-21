using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using LogViewer.Core.Services;
using LogViewer.Core.State;
using LogViewer.Helpers;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.TreeView;
using LogViewer.Services;
using NLog;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Logger tree and Don't Show / Don't Receive exclusions.
    /// HashSets live in Core <c>LoggerFilterState</c>; the VM only maps Node → FullPath.
    /// </summary>
    public sealed class LoggerTreeViewModel : BaseViewModel, IResettable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly LogViewState _state;
        private readonly FilterCoordinator _filter;
        private readonly LogSession _session;
        private readonly LogProcessingService _processing;
        private readonly LogFileWatchService _watch;
        private readonly LoggerTreeBuilder _builder;
        private readonly LoggerTreeMarker _marker;
        private readonly IAppSettings _settings;
        private readonly ReceiversViewModel _receivers;
        private readonly ImportViewModel _import;
        private readonly Action _requestClean;
        private readonly HashSet<string> _availableLoggers = new HashSet<string>();
        private bool _isSearchLoggersProcess;
        private bool _isEnableClearSearchLoggers;
        private string _searchLoggerText = string.Empty;
        private string _loggerHighlightText = string.Empty;
        private Node _selectedNode;
        private bool _treeCheckJustDone;
        private readonly List<Node> _exceptParents = new List<Node>();

        private RelayCommand _treeCheckCommand;
        private RelayCommand _clearLoggersCommand;
        private RelayCommand _collapseLoggersCommand;
        private RelayCommand _expandChildrenCommand;
        private RelayCommand _collapseChildrenCommand;
        private RelayCommand _clearChildrenCommand;
        private RelayCommand _showOnlyCommand;
        private RelayCommand _dontReceiveCommand;
        private RelayCommand _ignoreIpCommand;
        private RelayCommand _dontShowCommand;
        private RelayCommand _searchLoggersCommand;
        private RelayCommand _clearSearchLoggersCommand;
        private RelayCommand _toggleMarkCommand;
        private RelayCommand _findInTreeCommand;

        /// <summary>
        /// Display exclusions captured before Clean's async SessionCleared wipes LoggerFilterState.
        /// Don't Receive is not queued — it must stay off until AddEntries finishes.
        /// </summary>
        private List<string> _pendingExcluded;
        private List<string> _pendingIncluded;

        public LoggerTreeViewModel(
            LogViewState state,
            FilterCoordinator filter,
            LogSession session,
            LogProcessingService processing,
            LogFileWatchService watch,
            LoggerTreeBuilder builder,
            LoggerTreeMarker marker,
            IAppSettings settings,
            ReceiversViewModel receivers,
            ImportViewModel import,
            Action requestClean)
        {
            _state = state;
            _filter = filter;
            _session = session;
            _processing = processing;
            _watch = watch;
            _builder = builder;
            _marker = marker;
            _settings = settings;
            _receivers = receivers;
            _import = import;
            _requestClean = requestClean;
            Loggers.Add(_builder.CreateRoot());
            _state.SelectedLogChanged += (sender, args) => UpdateSelectedNode();
        }

        /// <summary>Single-Root collection. TreeView ItemsSource.</summary>
        public AsyncObservableCollection<Node> Loggers { get; } = new AsyncObservableCollection<Node>();

        /// <summary>
        /// After a checkbox click the presenter restores a nearby row, not the first visible one.
        /// Cleared in <c>LogSessionPresenter.OnFilteredViewUpdated</c>.
        /// </summary>
        public bool TreeCheckJustDone
        {
            get => _treeCheckJustDone;
            set => _treeCheckJustDone = value;
        }

        /// <summary>Highlight the tree branch when SelectedLog changes. Clears IsSelected on the previous node.</summary>
        public Node SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != null)
                    UpdateNodeSelections(_selectedNode, false);
                _selectedNode = value;
                if (_selectedNode != null)
                    UpdateNodeSelections(_selectedNode, true);
                OnPropertyChanged();
            }
        }

        /// <summary>Clear button for tree search.</summary>
        public bool IsEnableClearSearchLoggers
        {
            get => _isEnableClearSearchLoggers;
            set
            {
                _isEnableClearSearchLoggers = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Node filter. An empty string does not search but still clears the Clear button.</summary>
        public string SearchLoggerText
        {
            get => _searchLoggerText;
            set
            {
                _searchLoggerText = value;
                IsEnableClearSearchLoggers = _isSearchLoggersProcess || _searchLoggerText.Any();
                OnPropertyChanged();
            }
        }

        /// <summary>Highlight in the tree SearchableTextControl — a separate property so typing does not search on every character.</summary>
        public string LoggerHighlightText
        {
            get => _loggerHighlightText;
            set
            {
                _loggerHighlightText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Node checkbox: Don't Show / show again. CommandParameter is Node, not CheckBox.</summary>
        public RelayCommand TreeViewElementCheckCommand => _treeCheckCommand ?? (_treeCheckCommand = new RelayCommand(TreeViewElementCheck));
        public RelayCommand ClearLoggersCommand => _clearLoggersCommand ?? (_clearLoggersCommand = new RelayCommand(ClearLoggers));
        public RelayCommand CollapseLoggersCommand => _collapseLoggersCommand ?? (_collapseLoggersCommand = new RelayCommand(CollapseAllLoggers));
        public RelayCommand ExpandChildrenCommand => _expandChildrenCommand ?? (_expandChildrenCommand = new RelayCommand(ExpandTreeViewChildren));
        public RelayCommand CollapseChildrenCommand => _collapseChildrenCommand ?? (_collapseChildrenCommand = new RelayCommand(CollapseTreeViewChildren));
        public RelayCommand ClearChildrenCommand => _clearChildrenCommand ?? (_clearChildrenCommand = new RelayCommand(ClearChildrenLoggers));
        public RelayCommand ShowOnlyThisLoggerCommand => _showOnlyCommand ?? (_showOnlyCommand = new RelayCommand(ShowOnlyThisLogger));
        /// <summary>Context menu: Don't Receive. Removes rows already in the session.</summary>
        public RelayCommand DontReceiveThisLoggerCommand => _dontReceiveCommand ?? (_dontReceiveCommand = new RelayCommand(DontReceiveThisLogger));
        public RelayCommand IgnoreThisIPCommand => _ignoreIpCommand ?? (_ignoreIpCommand = new RelayCommand(IgnoreThisIP));
        public RelayCommand DontShowThisLoggerCommand => _dontShowCommand ?? (_dontShowCommand = new RelayCommand(DontShowThisLogger));
        public RelayCommand SearchLoggersCommand => _searchLoggersCommand ?? (_searchLoggersCommand = new RelayCommand(SearchLoggers));
        public RelayCommand ClearSearchLoggerResultCommand => _clearSearchLoggersCommand ?? (_clearSearchLoggersCommand = new RelayCommand(ClearSearchLoggersResult));
        public RelayCommand ToggleMarkCommand => _toggleMarkCommand ?? (_toggleMarkCommand = new RelayCommand(obj => _marker.Toggle(obj as Node)));
        public RelayCommand FindInTreeCommand => _findInTreeCommand ?? (_findInTreeCommand = new RelayCommand(FindLoggerInTreeByMessage));

        /// <summary>For tests: mark paths as already built without running BuildTreeByMessage.</summary>
        internal void SeedAvailableLoggers(IEnumerable<string> paths)
        {
            if (paths == null)
                return;
            foreach (var path in paths)
                _availableLoggers.Add(path);
        }

        /// <summary>
        /// Synchronous from <c>LogViewModel.Clean()</c>, before Core confirms an empty session.
        /// The tree is cleared immediately so the button does not look stuck. Display exclusions are cleared here too,
        /// in case async SessionCleared arrives later or is reordered. Don't Receive is kept
        /// (<c>keepDontReceive: true</c>) — otherwise after Clean those loggers would fill the buffer again.
        /// </summary>
        public void Reset()
        {
            ClearLoggersUi(keepDontReceive: true);
            _filter.Loggers.ClearDisplayExclusions();
            _filter.Apply();
        }

        /// <summary>Clear the "already seen this FullPath" cache. After that BuildTreeByMessage will create nodes again.</summary>
        public void ClearAvailable()
        {
            _availableLoggers.Clear();
        }

        /// <summary>
        /// When Core sent SessionChanged(Cleared). Independent of <see cref="Reset"/>:
        /// same end UI whichever order the calls arrive in.
        /// A queued session restore wins: Clean posts this after Open already wrote Don't Show.
        /// </summary>
        public void OnSessionCleared()
        {
            _availableLoggers.Clear();
            var root = Loggers[0];
            root.Children.Clear();
            if (!TryApplyQueuedDisplayRestore())
            {
                _filter.Loggers.ClearDisplayExclusions();
                _filter.Apply();
            }
        }

        /// <summary>
        /// Remember Don't Show / include-only so async SessionCleared cannot drop them during Open session.
        /// </summary>
        public void QueueDisplayFilterRestore()
        {
            _pendingExcluded = _filter.Loggers.ExcludedPaths.ToList();
            _pendingIncluded = _filter.Loggers.IncludeOnlyPaths.ToList();
        }

        /// <summary>Re-apply queued Don't Show after the tree exists. No-op when nothing is queued.</summary>
        public bool TryApplyQueuedDisplayRestore()
        {
            if (_pendingExcluded == null && _pendingIncluded == null)
                return false;
            _filter.Loggers.RestoreFromSession(_pendingExcluded, null, _pendingIncluded);
            _pendingExcluded = null;
            _pendingIncluded = null;
            _filter.Apply();
            SyncCheckboxesFromExclusions();
            return true;
        }

        /// <summary>Don't Receive rebuilt the Core hierarchy — redraw the WPF tree with the same checkboxes.</summary>
        public void RebuildFromCore()
        {
            _builder.RebuildFromCore(Loggers[0], _session.GetLoggerHierarchy(), _filter.Loggers);
        }

        /// <summary>
        /// After a full tree rebuild (import / Open session), remember FullPaths so live UDP
        /// does not create duplicate nodes.
        /// </summary>
        public void RememberPathsFromTree()
        {
            _availableLoggers.Clear();
            if (Loggers.Count == 0)
                return;
            CollectLoggerPaths(Loggers[0], _availableLoggers);
        }

        private static void CollectLoggerPaths(Node node, HashSet<string> paths)
        {
            if (node == null)
                return;
            if (!string.IsNullOrEmpty(node.Logger) && node.Logger != "Root")
                paths.Add(node.Logger);
            foreach (var child in node.Children)
                CollectLoggerPaths(child, paths);
        }

        /// <summary>
        /// Hot receive path: append a node if FullPath is new. Already seen paths only get a mark.
        /// </summary>
        public void BuildTreeByMessage(LogMessage log)
        {
            var root = Loggers[0].Children.FirstOrDefault(x => x.Text == log.Address);
            if (root == null)
            {
                var rootNode = new Node(Loggers[0], log.Address)
                {
                    IsRoot = true,
                    IsExpanded = true,
                    Logger = log.Address,
                    IsChecked = Loggers[0].IsChecked.HasValue && Loggers[0].IsChecked.Value,
                    IsVisible = !_isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper()),
                    Source = log.Address,
                };
                Loggers[0].Children.Add(rootNode);
                root = rootNode;
            }

            _marker.ApplyExistingMark(log, GetNodeFromMessage(log));

            // Already saw this FullPath — do not create nodes or touch exclusions on the hot UDP path.
            if (_availableLoggers.Contains(log.FullPath))
                return;

            Node currentParent = GetParentFromFullPath(root, log.Logger, log.ExecutableName);

            bool parentHidden = Loggers[0].IsChecked.HasValue && !Loggers[0].IsChecked.Value ||
                currentParent.IsChecked.HasValue && !currentParent.IsChecked.Value || !currentParent.IsChecked.HasValue;
            // Preset / Show only may name a logger that is not in the tree yet — hide it when it first appears.
            bool presetHidden = _filter.Loggers.ShouldHideNewLogger(log.FullPath);
            if (parentHidden || presetHidden)
            {
                // Parent is hidden: the new logger must not appear in the list (or in the buffer if Don't Receive).
                _filter.Loggers.Exclude(log.FullPath);
                if (!_session.FilterCriteria.ShouldStoreInBuffer(log.FullPath))
                    _filter.Loggers.ExcludeFromBuffer(log.FullPath);
                _filter.Apply();
            }

            _availableLoggers.Add(log.FullPath);

            var nodes = log.Logger.Split('.').ToList();

            if (!string.IsNullOrEmpty(log.ExecutableName))
            {
                var exe = root.Children.FirstOrDefault(x => x.Text == log.ExecutableName);
                if (exe == null)
                {
                    var exeNode = new Node(root, log.ExecutableName)
                    {
                        IsChecked = root.IsChecked,
                        IsVisible = !_isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper())
                    };
                    root.Children.Add(exeNode);
                    root = exeNode;
                }
                else
                    root = exe;
            }

            var parent = root;
            foreach (var node in nodes)
            {
                var prevParent = parent;
                parent = parent.Children.FirstOrDefault(x => x.Text == node);
                if (parent != null) continue;
                var newNode = new Node(prevParent, node)
                {
                    IsChecked = prevParent.IsChecked.HasValue && prevParent.IsChecked.Value,
                    IsVisible = !_isSearchLoggersProcess || log.Address.ToUpper().Contains(SearchLoggerText.ToUpper()),
                };
                prevParent.Children.Add(newNode);
                parent = newNode;
            }

            if (parent != null && (parentHidden || presetHidden))
                parent.IsChecked = false;
        }

        /// <summary>
        /// Highest checked FullPaths in the WPF tree: a node is a root when it is checked and its
        /// parent is mixed or unchecked. Empty when Root is fully checked (show every logger).
        /// </summary>
        public List<string> CollectIncludedRoots()
        {
            var result = new List<string>();
            if (Loggers.Count == 0)
                return result;
            var root = Loggers[0];
            if (root.IsChecked == true)
                return result;
            CollectIncludedRoots(root, result, isRoot: true);
            return result;
        }

        private static void CollectIncludedRoots(Node node, List<string> result, bool isRoot)
        {
            if (node == null)
                return;
            if (!isRoot && node.IsChecked == true)
            {
                if (!string.IsNullOrEmpty(node.Logger) && node.Logger != "Root")
                {
                    result.Add(node.Logger);
                    return;
                }
            }

            foreach (var child in node.Children)
            {
                if (child.IsChecked == false)
                    continue;
                CollectIncludedRoots(child, result, isRoot: false);
            }
        }

        /// <summary>Known FullPaths currently in the tree — used when a preset include-only list is applied.</summary>
        public IReadOnlyCollection<string> AvailableLoggerPaths => _availableLoggers;

        /// <summary>
        /// Every node with IsChecked == false (Don't Show). Session files store these FullPaths so Open
        /// does not depend on portable keys or on Root.IsChecked.
        /// </summary>
        public List<string> CollectUncheckedLoggerPaths()
        {
            var result = new List<string>();
            if (Loggers.Count == 0)
                return result;
            CollectUncheckedLoggerPaths(Loggers[0], result, isRoot: true);
            return result;
        }

        private static void CollectUncheckedLoggerPaths(Node node, List<string> result, bool isRoot)
        {
            if (node == null)
                return;
            if (!isRoot && node.IsChecked == false && !string.IsNullOrEmpty(node.Logger) && node.Logger != "Root")
                result.Add(node.Logger);
            foreach (var child in node.Children)
                CollectUncheckedLoggerPaths(child, result, isRoot: false);
        }

        /// <summary>
        /// Restore tree checkboxes from the active preset. Included leaves are checked, their ancestors
        /// are mixed/checked, everything else is unchecked. Root stays checked/mixed so new UDP loggers
        /// are not auto-hidden. Clears <see cref="CheckBoxId"/> so parent-unchecked does not cascade.
        /// </summary>
        public void SyncCheckboxesFromExclusions()
        {
            if (Loggers.Count == 0)
                return;
            CheckBoxId.CurrentСheckBoxId = null;
            var included = _filter.Loggers.IncludeOnlyPaths;
            var excluded = _filter.Loggers.ExcludedPaths;
            SyncNodeCheckbox(Loggers[0], included, excluded, isRoot: true);
        }

        private static bool? SyncNodeCheckbox(
            Node node,
            IReadOnlyCollection<string> included,
            IReadOnlyCollection<string> excluded,
            bool isRoot)
        {
            if (node == null)
                return false;

            bool includeOnly = included != null && included.Count > 0;
            bool selfKept = isRoot
                || (includeOnly
                    ? FilterPresetMapper.IsKeptByIncludeOnly(node.Logger, included)
                    : !IsExcluded(excluded, node.Logger));

            if (node.Children.Count == 0)
            {
                node.IsChecked = selfKept;
                if (selfKept && !isRoot)
                    ExpandAncestors(node);
                return selfKept;
            }

            int checkedCount = 0;
            int mixedCount = 0;
            foreach (var child in node.Children)
            {
                bool? childState = SyncNodeCheckbox(child, included, excluded, isRoot: false);
                if (childState == true)
                    checkedCount++;
                else if (!childState.HasValue)
                    mixedCount++;
            }

            bool? state;
            if (isRoot)
            {
                if (checkedCount == node.Children.Count && mixedCount == 0)
                    state = true;
                else if (checkedCount == 0 && mixedCount == 0)
                    state = true;
                else
                    state = null;
            }
            else if (selfKept)
            {
                state = mixedCount > 0 || checkedCount != node.Children.Count ? (bool?)null : true;
            }
            else if (checkedCount == 0 && mixedCount == 0)
            {
                state = false;
            }
            else if (checkedCount == node.Children.Count && mixedCount == 0)
            {
                state = true;
            }
            else
            {
                state = null;
            }

            node.IsChecked = state;
            if (state != false && !isRoot)
                node.IsExpanded = true;
            return state;
        }

        private static bool IsExcluded(IReadOnlyCollection<string> excluded, string path)
        {
            if (string.IsNullOrEmpty(path) || path == "Root" || excluded == null)
                return false;
            if (excluded.Contains(path))
                return true;
            foreach (var root in excluded)
            {
                if (LoggerFilterState.IsInSubtree(path, root))
                    return true;
            }
            return false;
        }

        private static void ExpandAncestors(Node node)
        {
            for (var current = node; current != null; current = current.Parent)
                current.IsExpanded = true;
        }

        private void TreeViewElementCheck(object obj)
        {
            var node = obj as Node;
            if (node == null)
                return;

            _state.IsBusy = true;
            try
            {
                _state.CaptureSelectionAnchor();

                if (node.IsChecked.HasValue && node.IsChecked.Value)
                {
                    if (node.Parent == null)
                        _filter.Loggers.ClearAll();
                    else
                        _filter.Loggers.IncludeSubtree(node.Logger, CollectChildPaths(node));
                }
                else
                    _filter.Loggers.ExcludeSubtree(node.Logger, CollectChildPaths(node));

                _filter.Apply();
                _treeCheckJustDone = true;
            }
            finally
            {
                _state.IsBusy = false;
            }
        }

        private void ClearLoggers()
        {
            ClearLoggersUi(keepDontReceive: false);
            _filter.Loggers.ClearAll();
            _filter.Apply();
        }

        private void ClearLoggersUi(bool keepDontReceive)
        {
            var isCheckedRoot = Loggers.First().IsChecked;
            var isExpandedRoot = Loggers.First().IsExpanded;
            Loggers.Clear();
            _availableLoggers.Clear();
            if (!keepDontReceive)
                _marker.ResetCount();
            Loggers.Add(_builder.CreateRoot(isExpandedRoot, isCheckedRoot));
        }

        private void ExpandTreeViewChildren(object obj)
        {
            var node = obj as Node;
            if (node == null) return;
            node.IsExpanded = true;
            if (node.Children.Any())
                ExpandChild(node.Children.ToList());
        }

        private void CollapseTreeViewChildren(object obj)
        {
            var node = obj as Node;
            if (node == null) return;

            if (node.Parent == null && node.Logger == "Root")
            {
                CollapseAllLoggers();
                return;
            }

            node.IsExpanded = false;
            if (node.Children.Any())
                CollapseChild(node.Children.ToList());
        }

        private void CollapseAllLoggers()
        {
            foreach (var child in Loggers[0].Children)
                CollapseTreeViewChildren(child);
        }

        private void ClearChildrenLoggers(object obj)
        {
            Logger.Debug($"ClearChildrenLoggers with {obj}");
            var node = obj as Node;
            if (node == null) return;
            try
            {
                _state.IsBusy = true;
                var watched = _watch.Find(node.Text, node.Logger);
                if (watched != null)
                    _watch.Remove(watched);

                if (node.Parent != null)
                {
                    var paths = new List<string>();
                    CollectPaths(node, paths);
                    _filter.Loggers.ForgetSubtree(paths);
                    foreach (var p in paths)
                        _availableLoggers.Remove(p);
                    _filter.Apply();
                    _session.RemoveEntriesByLogger(node.Logger);
                    node.Parent.Children.Remove(node);
                }
                else
                {
                    ClearLoggers();
                    _requestClean?.Invoke();
                }

                _import.RemoveImportPath(node.Logger);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"An error occurred while ClearChildrenLoggers with {node}");
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                _state.IsBusy = false;
            }
        }

        private void ShowOnlyThisLogger(object obj)
        {
            Logger.Debug($"ShowOnlyThisLogger with {obj}");
            var node = obj as Node;
            if (node == null) return;
            try
            {
                _state.CaptureSelectionAnchor();
                _state.IsBusy = true;
                CheckLoggers(node);
                if (node.Logger != "Root")
                    UncheckAllLoggers(Loggers.First(), node.Logger);
                _filter.Loggers.ShowOnly(node.Logger, _availableLoggers);
                _filter.Apply();
                _treeCheckJustDone = true;
            }
            catch (Exception e)
            {
                Logger.Warn(e, "An error occurred while ShowOnlyThisLogger.");
            }
            finally
            {
                _state.IsBusy = false;
                _state.SelectedLog = _state.GetLastSelectedOrNearby();
            }
        }

        private void DontReceiveThisLogger(object obj)
        {
            var node = SelectedNode;
            if (obj is Node paramNode)
                node = paramNode;
            if (node == null) return;

            DontReceiveRecursive(node);
            _filter.Apply();
            // The session still holds already received rows; without RemoveEntries they stay in AllLogs.
            _session.RemoveEntriesExcludedFromBuffer();
            _state.AllLogs = new AsyncObservableCollection<LogMessage>(
                _state.AllLogs.Where(m => _session.FilterCriteria.ShouldStoreInBuffer(m.FullPath)));
            _state.Logs = new AsyncObservableCollection<LogMessage>(
                _state.Logs.Where(m => _session.FilterCriteria.ShouldStoreInBuffer(m.FullPath)));
        }

        private void DontReceiveRecursive(Node node)
        {
            if (node == null) return;
            node.IsChecked = false;
            // childPaths=null: walk descendants via the WPF tree; Core does not know Node.
            _filter.Loggers.DontReceive(node.Logger, null);
            foreach (var nodeChild in node.Children)
                DontReceiveRecursive(nodeChild);
        }

        private void IgnoreThisIP(object obj)
        {
            Logger.Debug($"IgnoreThisIP with {obj}");
            var node = SelectedNode;
            if (obj is Node paramNode)
                node = paramNode;
            if (node == null || node.Parent == null && node.Text == "Root") return;

            HideSelectedNodeAndChild(node);

            while (!node.IsRoot)
                node = node.Parent;

            IPAddress.TryParse(node.Text, out IPAddress ip);
            if (ip == null)
                return;

            var existingIgnoreIP = _settings.IgnoredIPs.FirstOrDefault(x => x.IP == ip.ToString());
            if (existingIgnoreIP != null)
                existingIgnoreIP.IsActive = true;
            else
            {
                _settings.IgnoredIPs.Add(new IgnoredIPAddress
                {
                    IP = ip.ToString(),
                    IsActive = true
                });
            }

            _receivers.RecreateUdpSources();
            _settings.Save();
        }

        private void DontShowThisLogger(object obj)
        {
            try
            {
                if (SelectedNode != null)
                    UncheckAndHideLoggers(SelectedNode);
            }
            catch (Exception e)
            {
                Logger.Warn(e, "An error occurred while DontReceiveLogger");
            }
        }

        private void SearchLoggers()
        {
            if (string.IsNullOrEmpty(SearchLoggerText))
            {
                ClearSearchLoggersResult();
                return;
            }
            LoggerHighlightText = SearchLoggerText;
            _exceptParents.Clear();
            FindLastNodesAndUpdateVisibility(Loggers[0]);
            _exceptParents.ForEach(x => x.IsVisible = true);
        }

        private void ClearSearchLoggersResult()
        {
            if (_isSearchLoggersProcess)
            {
                SetAllLoggersVisible(Loggers[0]);
                SearchLoggerText = string.Empty;
                LoggerHighlightText = string.Empty;
                _isSearchLoggersProcess = false;
                IsEnableClearSearchLoggers = false;
            }
            SearchLoggerText = string.Empty;
            LoggerHighlightText = string.Empty;
        }

        private void FindLoggerInTreeByMessage()
        {
            if (_state.SelectedLog == null)
                return;
            SearchLoggerText = _state.SelectedLog.FullPath;
            _exceptParents.Clear();
            FindLastNodesAndUpdateVisibility(Loggers[0], true);
            _exceptParents.ForEach(x => x.IsVisible = true);
        }

        private void CheckLoggers(Node node)
        {
            node.IsChecked = true;
            foreach (var nodeChild in node.Children)
                CheckLoggers(nodeChild);
        }

        private void UncheckAndHideLoggers(Node node)
        {
            node.IsChecked = false;
            _filter.Loggers.ExcludeSubtree(node.Logger, null);
            _filter.Apply();
        }

        private void UncheckAllLoggers(Node node, string exceptLogger = null)
        {
            if (node.Logger == exceptLogger)
                return;
            node.IsChecked = false;
            foreach (var nodeChild in node.Children)
                UncheckAllLoggers(nodeChild, exceptLogger);
        }

        private void HideSelectedNodeAndChild(Node node)
        {
            try
            {
                _state.CaptureSelectionAnchor();
                _state.IsBusy = true;
                UncheckAllLoggers(node);
                DontReceiveThisLogger(node);
            }
            catch (Exception e)
            {
                Logger.Warn(e, "An error occurred while ShowOnlyThisLogger.");
            }
            finally
            {
                _state.IsBusy = false;
                _state.SelectedLog = _state.GetLastSelectedOrNearby();
            }
        }

        private void UpdateSelectedNode()
        {
            try
            {
                if (_state.SelectedLog == null)
                    return;
                var selected = _state.SelectedLog;
                var nodes = string.IsNullOrEmpty(selected.ExecutableName)
                    ? new List<string> { selected.Address }
                    : new List<string> { selected.Address, selected.ExecutableName };
                nodes.AddRange(selected.Logger.Split('.'));
                if (!nodes.Any())
                    return;

                var foundNode = Loggers[0].Children.FirstOrDefault(x => x.Text == nodes[0] || x.Source == nodes[0]);
                if (foundNode == null) return;

                for (int i = 1; i < nodes.Count; i++)
                {
                    var node = foundNode.Children.FirstOrDefault(x => x.Text == nodes[i]);
                    if (node != null)
                        foundNode = node;
                }
                if (foundNode.Text != nodes.Last())
                    return;
                SelectedNode = foundNode;
            }
            catch (Exception e)
            {
                Logger.Warn(e, $"An error occurred while UpdateSelectedNode. With {_state.SelectedLog.FullPath}");
            }
        }

        private static void UpdateNodeSelections(Node node, bool isSelect)
        {
            node.IsSelected = isSelect;
            if (node.Parent != null)
                UpdateNodeSelections(node.Parent, isSelect);
        }

        private static Node GetParentFromFullPath(Node root, string logger, string executableName = null)
        {
            List<string> nodesStr = new List<string>();
            if (!string.IsNullOrEmpty(executableName))
                nodesStr.Add(executableName);
            nodesStr.AddRange(logger.Split('.'));

            var parent = root;
            foreach (var node in nodesStr)
            {
                var prevParent = parent;
                parent = parent.Children.FirstOrDefault(x => x.Text == node);
                if (parent != null) continue;
                return prevParent;
            }

            return root;
        }

        private Node GetNodeFromMessage(LogMessage message)
        {
            Node currentNode = Loggers[0].Children.FirstOrDefault(x => x.Text == message.Address);
            Node foundNode = currentNode;
            if (currentNode == null)
                return null;

            List<string> nodesStr = new List<string>();
            if (!string.IsNullOrEmpty(message.ExecutableName))
                nodesStr.Add(message.ExecutableName);
            nodesStr.AddRange(message.Logger.Split('.'));

            foreach (var nodeName in nodesStr)
            {
                currentNode = currentNode.Children.FirstOrDefault(x => x.Text == nodeName);
                if (currentNode == null)
                    return foundNode;
                foundNode = currentNode;
            }
            return foundNode;
        }

        private void UpdateLoggersVisibility(Node node, bool fullPath = false)
        {
            if (_exceptParents.Contains(node))
                return;

            if (fullPath && node.Logger.ToUpper().Contains(SearchLoggerText.ToUpper()) || CheckChildContainsText(node.Children, fullPath) ||
                node.Text.ToUpper().Contains(SearchLoggerText.ToUpper()))
            {
                node.IsVisible = true;
                AddParentsToExcept(node);
                return;
            }

            IsEnableClearSearchLoggers = true;
            _isSearchLoggersProcess = true;
            node.IsVisible = false;
            if (node.Parent?.Parent != null)
                UpdateLoggersVisibility(node.Parent, fullPath);
        }

        private void AddParentsToExcept(Node node)
        {
            if (node == null || _exceptParents.Contains(node)) return;
            _exceptParents.Add(node);
            if (node.Parent != null) AddParentsToExcept(node.Parent);
        }

        private bool CheckChildContainsText(ObservableCollection<Node> nodeChildren, bool fullPath = false)
        {
            if (!nodeChildren.Any())
                return false;

            foreach (var nodeChild in nodeChildren)
            {
                if (fullPath && nodeChild.Logger.ToUpper().Contains(SearchLoggerText.ToUpper()) ||
                    nodeChild.Text.ToUpper().Contains(SearchLoggerText.ToUpper()))
                    return true;
                CheckChildContainsText(nodeChild.Children);
            }

            return false;
        }

        /// <summary>Tree search: leaves that miss the query are hidden; matching parents stay visible.</summary>
        public void FindLastNodesAndUpdateVisibility(Node node, bool fullPath = false)
        {
            if (node.Children.Any())
            {
                foreach (var child in node.Children)
                {
                    child.IsVisible = true;
                    FindLastNodesAndUpdateVisibility(child, fullPath);
                }
            }
            else
                UpdateLoggersVisibility(node, fullPath);
        }

        private static void SetAllLoggersVisible(Node node)
        {
            node.IsVisible = true;
            foreach (var nodeChild in node.Children)
                SetAllLoggersVisible(nodeChild);
        }

        private static void ExpandChild(List<Node> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = true;
                if (node.Children.Any())
                    ExpandChild(node.Children.ToList());
            }
        }

        private static void CollapseChild(List<Node> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = false;
                if (node.Children.Any())
                    CollapseChild(node.Children.ToList());
            }
        }

        private static List<string> CollectChildPaths(Node node)
        {
            var paths = new List<string>();
            foreach (var child in node.Children)
                CollectPaths(child, paths);
            return paths;
        }

        private static void CollectPaths(Node node, List<string> paths)
        {
            paths.Add(node.Logger);
            foreach (var child in node.Children)
                CollectPaths(child, paths);
        }
    }
}
