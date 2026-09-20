using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using LogViewer.Adapters;
using LogViewer.Core.Abstractions;
using LogViewer.Core.Services;
using LogViewer.Factories;
using LogViewer.Helpers;
using LogViewer.Localization;
using LogViewer.MVVM.Commands;
using LogViewer.MVVM.Models;
using LogViewer.MVVM.ViewModels.Log;
using LogViewer.Services;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Start/pause live UDP/TCP. <see cref="LogSourceFactory"/> creates sources; the VM only holds sockets.
    /// </summary>
    public sealed class ReceiversViewModel : BaseViewModel, IResettable
    {
        private const int ReceiverColumnWidth = 15;
        private readonly LogProcessingService _processing;
        private readonly CoreToUiAdapter _adapter;
        private readonly FilterCoordinator _filter;
        private readonly LogSourceFactory _sourceFactory;
        private readonly IDialogService _dialogs;
        private readonly List<INetworkLogSource> _networkSources = new List<INetworkLogSource>();
        private readonly List<Receiver> _receivers;
        private bool _startIsEnabled = true;
        private int _colorColumnWidth;
        private RelayCommand _startCommand;
        private RelayCommand _pauseCommand;

        public ReceiversViewModel(
            LogProcessingService processing,
            CoreToUiAdapter adapter,
            FilterCoordinator filter,
            LogSourceFactory sourceFactory,
            IDialogService dialogs,
            List<Receiver> receivers)
        {
            _processing = processing;
            _adapter = adapter;
            _filter = filter;
            _sourceFactory = sourceFactory;
            _dialogs = dialogs;
            _receivers = receivers;
            RecreateUdpSources();
            RefreshColorColumnWidth();
        }

        /// <summary>The same list as in Settings: do not copy; the projector reads colors from here.</summary>
        public List<Receiver> Receivers => _receivers;

        /// <summary>true = receive is stopped, Start is shown. false = sockets run, Pause is shown.</summary>
        public bool StartIsEnabled
        {
            get => _startIsEnabled;
            set
            {
                _startIsEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PauseIsEnabled));
            }
        }

        /// <summary>Inverse of Start for the Pause button binding (the binding used to be broken).</summary>
        public bool PauseIsEnabled => !_startIsEnabled;

        /// <summary>0 hides the color column (one receiver or all white).</summary>
        public int ColorReceiverColumnWidth
        {
            get => _colorColumnWidth;
            set
            {
                _colorColumnWidth = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand StartCommand => _startCommand ?? (_startCommand = new RelayCommand(Start));
        public RelayCommand PauseCommand => _pauseCommand ?? (_pauseCommand = new RelayCommand(Pause));

        /// <summary>Start all live sources. Apply the filter first so Don't Receive is in effect from the first packet.</summary>
        public void Start()
        {
            if (!_networkSources.Any())
            {
                _dialogs.ShowInformation(Locals.NoReceiversMessageBoxInfo, Locals.Information);
                return;
            }
            _filter.Apply();
            _processing.StartAllSources();
            StartIsEnabled = false;
        }

        /// <summary>Stop sockets and flush the batcher, otherwise more rows appear after Pause.</summary>
        public void Pause()
        {
            _processing.StopAllSources();
            _adapter.FlushPending();
            StartIsEnabled = true;
        }

        /// <summary>After settings: new ports/transport/ignore-IP. Empty list is a no-op; file sources are not touched.</summary>
        public void RecreateUdpSources()
        {
            var results = _sourceFactory.CreateFromSettings(_receivers);
            if (results == null || !results.Any())
                return;

            // Historically: a non-empty live list removes all sources, including file follow.
            _processing.RemoveAllSources();
            _networkSources.Clear();

            foreach (var result in results)
            {
                string errorMessage;
                if (!result.Source.TryInit(out errorMessage))
                {
                    _dialogs.ShowError(string.Format(Locals.PortIsBusy, result.Receiver.Port), Locals.Error);
                    continue;
                }
                _processing.AddSource(result.Source);
                _networkSources.Add(result.Source);
            }
        }

        /// <summary>Column width at start: compares Color, not ColorString.</summary>
        public void RefreshColorColumnWidth()
        {
            ColorReceiverColumnWidth = _receivers.Count == 1
                || _receivers.Where(r => r.IsActive).All(x => x.Color.Color == Colors.White)
                ? 0
                : ReceiverColumnWidth;
        }

        /// <summary>After the settings window, Color may not yet be applied to the Brush — compare ColorString.</summary>
        public void RefreshColorColumnWidthAfterSettings()
        {
            ColorReceiverColumnWidth = _receivers.Count(res => res.IsActive) == 1
                || _receivers.Where(r => r.IsActive).All(x => x.ColorString == Colors.White.ToString())
                ? 0
                : ReceiverColumnWidth;
        }

        /// <inheritdoc />
        public void Reset()
        {
            // Clean does not tear down live sockets: the user expects receive to continue on an empty list.
        }
    }
}
