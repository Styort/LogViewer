using System;
using System.Collections.Generic;
using System.Linq;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels;
using LogViewer.Enums;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Shared lists and selection for the main window.
    /// Child VMs do not talk to the host: they only read/write this state.
    /// </summary>
    public sealed class LogViewState : BaseViewModel
    {
        private AsyncObservableCollection<LogMessage> _allLogs = new AsyncObservableCollection<LogMessage>();
        private AsyncObservableCollection<LogMessage> _logs = new AsyncObservableCollection<LogMessage>();
        private LogMessage _selectedLog;
        private List<LogMessage> _selectedLogs = new List<LogMessage>();
        private bool _isBusy;
        private bool _isEnableLogList = true;
        private LogMessage _lastLogMessage;
        private readonly List<LogMessage> _nearbyLastLogMessages = new List<LogMessage>();

        /// <summary>
        /// Filtered list changed. Timeline and bookmarks subscribe here, not to the host.
        /// </summary>
        public event EventHandler LogsChanged;

        /// <summary>Full UI buffer changed (Don't Receive / Clean / max-buffer trim).</summary>
        public event EventHandler AllLogsChanged;

        /// <summary>Single-row selection. The tree highlights the node; search enables Find Previous.</summary>
        public event EventHandler SelectedLogChanged;

        /// <summary>All received rows, including those hidden by Don't Show. Don't Receive never lands here.</summary>
        public AsyncObservableCollection<LogMessage> AllLogs
        {
            get => _allLogs;
            set
            {
                _allLogs = value ?? new AsyncObservableCollection<LogMessage>();
                OnPropertyChanged();
                AllLogsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>What the ListView shows after the AND filter.</summary>
        public AsyncObservableCollection<LogMessage> Logs
        {
            get => _logs;
            set
            {
                _logs = value ?? new AsyncObservableCollection<LogMessage>();
                OnPropertyChanged();
                LogsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Current details row. The setter raises <see cref="SelectedLogChanged"/> even for the same reference — intentional for tree resync.</summary>
        public LogMessage SelectedLog
        {
            get => _selectedLog;
            set
            {
                _selectedLog = value;
                OnPropertyChanged();
                SelectedLogChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Multi-select in ListView (Ctrl+click). Copy uses Logs order, not click order.</summary>
        public List<LogMessage> SelectedLogs
        {
            get => _selectedLogs;
            set
            {
                _selectedLogs = value ?? new List<LogMessage>();
                OnPropertyChanged();
            }
        }

        /// <summary>Import/tree checkbox: the list is locked so users cannot click a half-built view.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                IsEnableLogList = !_isBusy;
                OnPropertyChanged();
            }
        }

        /// <summary>Inverse of <see cref="IsBusy"/> for IsEnabled on the ListView and tree.</summary>
        public bool IsEnableLogList
        {
            get => _isEnableLogList;
            set
            {
                _isEnableLogList = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Remember the current row and neighbors before a filter change / buffer trim,
        /// so after the list rebuild we jump nearby instead of to the start.
        /// </summary>
        public void CaptureSelectionAnchor()
        {
            LastLogMessage = SelectedLog;
        }

        /// <summary>
        /// Row after rebuild: the same one or a neighbor (including nearest Warn/Error); otherwise selection jumps to the start.
        /// </summary>
        public LogMessage GetLastSelectedOrNearby()
        {
            if (_logs == null)
                return null;
            if (_logs.Contains(_lastLogMessage))
                return _lastLogMessage;
            return _nearbyLastLogMessages.FirstOrDefault(m => _logs.Contains(m));
        }

        /// <summary>
        /// Copy in list order, not ListView click order.
        /// </summary>
        public List<LogMessage> GetLogsToCopy()
        {
            if (SelectedLogs != null && SelectedLogs.Count > 0)
            {
                var selected = new HashSet<LogMessage>(SelectedLogs);
                return Logs.Where(l => selected.Contains(l)).ToList();
            }

            if (SelectedLog != null)
                return new List<LogMessage> { SelectedLog };

            return new List<LogMessage>();
        }

        private LogMessage LastLogMessage
        {
            get => _lastLogMessage;
            set
            {
                _lastLogMessage = value;
                _nearbyLastLogMessages.Clear();
                if (Logs == null || _lastLogMessage == null)
                    return;
                var index = Logs.IndexOf(_lastLogMessage);
                if (index == -1)
                    return;
                if (index != 0)
                    _nearbyLastLogMessages.Add(Logs[index - 1]);
                if (Logs.Count > index + 1)
                    _nearbyLastLogMessages.Add(Logs[index + 1]);
                AddNear(index, eLogLevel.Debug);
                AddNear(index, eLogLevel.Warn);
                AddNear(index, eLogLevel.Error);
                AddNear(index, eLogLevel.Fatal);
            }
        }

        private void AddNear(int currentIndex, eLogLevel level)
        {
            var near = FindNearMessageByLogLevel(currentIndex, level);
            if (near != null)
                _nearbyLastLogMessages.Add(near);
        }

        private LogMessage FindNearMessageByLogLevel(int currentIndex, eLogLevel level)
        {
            if (Logs.Count <= currentIndex + 1 || currentIndex - 1 <= 0)
                return null;
            for (int i = currentIndex + 1; i < Logs.Count; i++)
            {
                if (Logs[i].Level == level)
                    return Logs[i];
            }

            for (int i = currentIndex - 1; i >= 0; i--)
            {
                if (Logs[i].Level == level)
                    return Logs[i];
            }

            return null;
        }
    }
}
