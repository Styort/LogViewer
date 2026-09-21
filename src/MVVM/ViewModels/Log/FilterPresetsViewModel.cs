using System;
using System.Collections.ObjectModel;
using System.Linq;
using LogViewer.Core.Domain;
using LogViewer.Core.Services;
using LogViewer.Enums;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.Services;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Named filter presets. Apply overwrites display criteria only (not the log buffer, not Don't Receive).
    /// Persistence is <c>filter_presets.xml</c> next to settings, never <c>settings.xml</c>.
    /// </summary>
    public sealed class FilterPresetsViewModel : BaseViewModel
    {
        private readonly FilterCoordinator _coordinator;
        private readonly IDialogService _dialogs;
        private readonly SearchViewModel _search;
        private readonly LoggerTreeViewModel _tree;
        private readonly Action<eLogLevel> _setMinLevelUi;
        private readonly string _filePath;
        private FilterPreset _selected;
        private string _draftName = string.Empty;
        private bool _leaveTimeIntervalUnchanged;
        private bool _saveAbsoluteTime = true;
        private bool _isRelativeTimeInterval;
        private string _relativeMinutesText = "15";
        private RelayCommand _openCommand;
        private RelayCommand _applyCommand;
        private RelayCommand _clearCommand;
        private RelayCommand _saveCommand;
        private RelayCommand _deleteCommand;
        private RelayCommand _renameCommand;
        private RelayCommand _helpCommand;
        private string _activePresetName;

        public FilterPresetsViewModel(
            FilterCoordinator coordinator,
            IDialogService dialogs,
            SearchViewModel search,
            LoggerTreeViewModel tree,
            Action<eLogLevel> setMinLevelUi,
            string filePath = null)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            _setMinLevelUi = setMinLevelUi ?? throw new ArgumentNullException(nameof(setMinLevelUi));
            _filePath = filePath ?? FilterPresetXmlStore.ResolveDefaultPath();
            ReloadFromDisk();
        }

        /// <summary>Raised after a successful Apply so the manager window can close.</summary>
        public event Action CloseRequested;

        public ObservableCollection<FilterPreset> Items { get; } = new ObservableCollection<FilterPreset>();

        public FilterPreset Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                if (value != null && string.IsNullOrWhiteSpace(DraftName))
                    DraftName = value.Name;
                OnPropertyChanged();
            }
        }

        public string DraftName
        {
            get => _draftName;
            set { _draftName = value ?? string.Empty; OnPropertyChanged(); }
        }

        public bool LeaveTimeIntervalUnchanged
        {
            get => _leaveTimeIntervalUnchanged;
            set { _leaveTimeIntervalUnchanged = value; OnPropertyChanged(); }
        }

        public bool SaveAbsoluteTime
        {
            get => _saveAbsoluteTime;
            set { _saveAbsoluteTime = value; OnPropertyChanged(); }
        }

        public bool IsRelativeTimeInterval
        {
            get => _isRelativeTimeInterval;
            set { _isRelativeTimeInterval = value; OnPropertyChanged(); }
        }

        public string RelativeMinutesText
        {
            get => _relativeMinutesText;
            set { _relativeMinutesText = value; OnPropertyChanged(); }
        }

        /// <summary>Name of the last applied preset, or null when none is active.</summary>
        public string ActivePresetName
        {
            get => _activePresetName;
            private set
            {
                if (string.Equals(_activePresetName, value, StringComparison.Ordinal))
                    return;
                _activePresetName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActivePreset));
            }
        }

        public bool HasActivePreset => !string.IsNullOrEmpty(_activePresetName);

        public RelayCommand OpenCommand => _openCommand ?? (_openCommand = new RelayCommand(_ => OpenManager()));
        public RelayCommand ApplyCommand => _applyCommand ?? (_applyCommand = new RelayCommand(_ => ApplySelected(), _ => Selected != null));
        public RelayCommand ClearCommand => _clearCommand ?? (_clearCommand = new RelayCommand(_ => ClearActive(), _ => HasActivePreset));
        public RelayCommand SaveCurrentCommand => _saveCommand ?? (_saveCommand = new RelayCommand(_ => SaveCurrent()));
        public RelayCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new RelayCommand(_ => DeleteSelected(), _ => Selected != null));
        public RelayCommand RenameCommand => _renameCommand ?? (_renameCommand = new RelayCommand(_ => RenameSelected(), _ => Selected != null));
        public RelayCommand HelpCommand => _helpCommand ?? (_helpCommand = new RelayCommand(_ =>
            _dialogs.ShowInformation(Locals.FilterPresetsDescription, Locals.FilterPresets)));

        public void ApplySelected()
        {
            if (Selected == null)
                return;
            ApplyPreset(Selected);
            CloseRequested?.Invoke();
        }

        public void ClearActive()
        {
            if (!HasActivePreset)
                return;
            _coordinator.ClearAppliedPreset();
            _search.LoadFromPreset(string.Empty, false, false, false, true, false);
            _setMinLevelUi(_coordinator.CurrentMinLevel);
            _tree.SyncCheckboxesFromExclusions();
            ActivePresetName = null;
            CloseRequested?.Invoke();
        }

        internal void ApplyPreset(FilterPreset preset)
        {
            _coordinator.ApplyPreset(preset, DateTime.Now, _tree.AvailableLoggerPaths);
            bool searchActive = _coordinator.IsSearchActive || _coordinator.IsTimeIntervalActive;
            _search.LoadFromPreset(
                preset.SearchText ?? string.Empty,
                preset.MatchCase,
                preset.MatchWholeWord,
                preset.UseRegex,
                FilterPresetMapper.ResolveMatchLogLevel(preset),
                searchActive);
            _setMinLevelUi(_coordinator.CurrentMinLevel);
            _tree.SyncCheckboxesFromExclusions();
            ActivePresetName = preset.Name;
        }

        private void OpenManager()
        {
            if (HasActivePreset)
            {
                var active = FindByName(ActivePresetName);
                if (active != null)
                    Selected = active;
            }
            _dialogs.ShowFilterPresets(this);
        }

        private void SaveCurrent()
        {
            string name = (DraftName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                _dialogs.ShowError(Locals.FilterPresetEmptyName, Locals.FilterPresets);
                return;
            }

            var existing = FindByName(name);
            if (existing != null && !_dialogs.Confirm(string.Format(Locals.FilterPresetOverwrite, name), Locals.FilterPresets))
                return;

            var preset = _coordinator.CapturePreset(name, _tree.AvailableLoggerPaths, _tree.CollectIncludedRoots());
            preset.LeaveTimeIntervalUnchanged = LeaveTimeIntervalUnchanged;
            preset.IsRelativeTimeInterval = IsRelativeTimeInterval && !LeaveTimeIntervalUnchanged;
            if (preset.IsRelativeTimeInterval)
            {
                preset.IsTimeIntervalActive = true;
                int minutes;
                preset.RelativeMinutes = int.TryParse(RelativeMinutesText, out minutes) && minutes > 0 ? minutes : 15;
            }

            if (existing != null)
            {
                int index = Items.IndexOf(existing);
                Items[index] = preset;
                Selected = preset;
            }
            else
            {
                Items.Add(preset);
                Selected = preset;
            }

            Persist();
        }

        private void DeleteSelected()
        {
            if (Selected == null)
                return;
            if (!_dialogs.Confirm(string.Format(Locals.FilterPresetDeleteConfirm, Selected.Name), Locals.FilterPresets))
                return;
            var removed = Selected;
            Items.Remove(removed);
            if (string.Equals(removed.Name, ActivePresetName, StringComparison.OrdinalIgnoreCase))
                ActivePresetName = null;
            Selected = Items.FirstOrDefault();
            Persist();
        }

        private void RenameSelected()
        {
            if (Selected == null)
                return;
            string text;
            if (!_dialogs.TryPromptText(Locals.FilterPresetRenameTitle, Locals.FilterPresetName, Selected.Name, out text))
                return;
            string name = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                _dialogs.ShowError(Locals.FilterPresetEmptyName, Locals.FilterPresets);
                return;
            }

            var clash = FindByName(name);
            if (clash != null && !ReferenceEquals(clash, Selected))
            {
                if (!_dialogs.Confirm(string.Format(Locals.FilterPresetOverwrite, name), Locals.FilterPresets))
                    return;
                Items.Remove(clash);
            }

            bool wasActive = string.Equals(Selected.Name, ActivePresetName, StringComparison.OrdinalIgnoreCase);
            Selected.Name = name;
            DraftName = name;
            if (wasActive)
                ActivePresetName = name;
            OnPropertyChanged(nameof(Selected));
            Persist();
            RefreshItems();
        }

        private FilterPreset FindByName(string name)
        {
            return Items.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private void ReloadFromDisk()
        {
            Items.Clear();
            var doc = FilterPresetXmlStore.LoadFile(_filePath);
            foreach (var preset in doc.Presets)
            {
                if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                    Items.Add(preset);
            }
            Selected = Items.FirstOrDefault();
            if (Selected != null)
                DraftName = Selected.Name;
        }

        private void Persist()
        {
            var doc = new FilterPresetDocument
            {
                Version = FilterPresetDocument.CurrentVersion,
                Presets = Items.ToList()
            };
            FilterPresetXmlStore.SaveFile(_filePath, doc);
        }

        private void RefreshItems()
        {
            var selected = Selected;
            var copy = Items.ToList();
            Items.Clear();
            foreach (var item in copy)
                Items.Add(item);
            Selected = selected != null ? FindByName(selected.Name) : null;
        }
    }
}
