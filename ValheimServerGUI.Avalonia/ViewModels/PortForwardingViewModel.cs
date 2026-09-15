using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Network;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Per-row forwarding state, used by the dialog to pick a colour: green when forwarded,
    /// amber when the port is known to be missing, grey when the gateway could not be read.
    /// </summary>
    public enum PortMappingRowState
    {
        Unknown,
        Mapped,
        NotMapped,
    }

    /// <summary>
    /// One UDP port row in the port-forwarding dialog.
    /// </summary>
    public partial class PortRowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isMapped;

        [ObservableProperty]
        private bool _mappingKnown;

        public PortRowViewModel(int port, bool isMapped, bool mappingKnown)
        {
            Port = port;
            IsMapped = isMapped;
            MappingKnown = mappingKnown;
        }

        public int Port { get; }

        public string Protocol => "UDP";

        public PortMappingRowState State => !MappingKnown
            ? PortMappingRowState.Unknown
            : IsMapped
                ? PortMappingRowState.Mapped
                : PortMappingRowState.NotMapped;

        public string StatusText => State switch
        {
            PortMappingRowState.Mapped => "Mapped",
            PortMappingRowState.NotMapped => "Not mapped",
            _ => "Unknown",
        };

        partial void OnIsMappedChanged(bool value) => OnStateChanged();

        partial void OnMappingKnownChanged(bool value) => OnStateChanged();

        private void OnStateChanged()
        {
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>
    /// Port forwarding dialog (UPnP / NAT-PMP): discovers the gateway and maps/removes the three
    /// adjacent UDP ports the Valheim server needs. Manual only — nothing is changed automatically.
    /// A check runs automatically when the dialog opens so the state is never shown as "not mapped"
    /// before it has actually been read.
    /// </summary>
    public partial class PortForwardingViewModel : ObservableObject
    {
        private readonly IPortForwarder PortForwarder;
        private readonly ServerControlsViewModel ServerControls;
        private readonly IApplicationLogger Logger;

        public ObservableCollection<PortRowViewModel> Ports { get; } = new();

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private PortForwardingSummary _summary = PortForwardingSummary.NotChecked;

        [ObservableProperty]
        private string? _gatewayText;

        [ObservableProperty]
        private string? _externalIpText;

        [ObservableProperty]
        private bool _showPrivateAddressWarning;

        [ObservableProperty]
        private string? _errorMessage;

        public int BasePort => ServerControls.Port;

        /// <summary>Glyph shown next to the summary: a green check when all ports are forwarded.</summary>
        public string SummaryIcon => Summary.State switch
        {
            PortForwardingState.AllMapped => "✓",
            PortForwardingState.PartiallyMapped => "!",
            PortForwardingState.NoneMapped => "!",
            PortForwardingState.NoGateway => "✕",
            _ => string.Empty,
        };

        public bool HasSummaryIcon => SummaryIcon.Length > 0;

        public PortForwardingViewModel(
            IPortForwarder portForwarder,
            ServerControlsViewModel serverControls,
            IApplicationLogger logger)
        {
            PortForwarder = portForwarder;
            ServerControls = serverControls;
            Logger = logger;

            ResetPorts();
        }

        /// <summary>
        /// Runs a gateway check. Called automatically when the dialog opens; also bound to the
        /// "Check ports" button.
        /// </summary>
        public Task CheckAsync() => DiscoverCommand.ExecuteAsync(null);

        [RelayCommand]
        private async Task DiscoverAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            Summary = new PortForwardingSummary { State = PortForwardingState.NotChecked, Text = "Searching for a gateway..." };

            try
            {
                var status = await PortForwarder.DiscoverAsync(GetRequests());

                if (!status.GatewayFound)
                {
                    GatewayText = null;
                    ExternalIpText = null;
                    ShowPrivateAddressWarning = false;
                    ErrorMessage = status.Error ?? "Make sure UPnP is enabled on your router.";
                    ResetPorts();
                    Summary = PortForwardingSummary.From(status);
                    return;
                }

                GatewayText = status.GatewayEndpoint;
                ExternalIpText = string.IsNullOrWhiteSpace(status.ExternalIpAddress) ? "Unknown" : status.ExternalIpAddress;
                ShowPrivateAddressWarning = status.IsExternalAddressPrivate;
                ApplyStates(status.Ports);
                Summary = PortForwardingSummary.From(status);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Port forwarding discovery failed");
                ErrorMessage = e.Message;
                Summary = PortForwardingSummary.Error("Port check failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanMap))]
        private async Task MapAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var result = await PortForwarder.MapAsync(GetRequests());
                ApplyStates(result.Ports);
                Summary = PortForwardingSummary.FromPorts(result.Ports);
                ErrorMessage = result.Success ? null : result.Error;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Port mapping failed");
                ErrorMessage = e.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRemove))]
        private async Task RemoveAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var result = await PortForwarder.RemoveAsync(GetRequests());
                ApplyStates(result.Ports);
                Summary = PortForwardingSummary.FromPorts(result.Ports);
                ErrorMessage = result.Success ? null : result.Error;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Port removal failed");
                ErrorMessage = e.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanMap()
        {
            return !IsBusy && Summary.State is PortForwardingState.PartiallyMapped
                or PortForwardingState.NoneMapped
                or PortForwardingState.Unknown;
        }

        private bool CanRemove() => !IsBusy && Summary.HasMapped;

        partial void OnSummaryChanged(PortForwardingSummary value)
        {
            OnPropertyChanged(nameof(SummaryIcon));
            OnPropertyChanged(nameof(HasSummaryIcon));
            MapCommand.NotifyCanExecuteChanged();
            RemoveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            MapCommand.NotifyCanExecuteChanged();
            RemoveCommand.NotifyCanExecuteChanged();
        }

        private IReadOnlyList<PortMappingRequest> GetRequests() => ValheimPorts.GetRequiredMappings(BasePort);

        private void ResetPorts()
        {
            ApplyStates(GetRequests().Select(r => new PortMappingState
            {
                Port = r.Port,
                ExternalPort = r.ExternalPort,
                Protocol = r.Protocol,
                IsMapped = false,
                MappingKnown = false,
            }).ToList());
        }

        private void ApplyStates(IReadOnlyList<PortMappingState> states)
        {
            Ports.Clear();

            if (states == null || states.Count == 0)
            {
                foreach (var request in GetRequests())
                {
                    Ports.Add(new PortRowViewModel(request.Port, false, false));
                }

                return;
            }

            foreach (var state in states.OrderBy(s => s.Port))
            {
                Ports.Add(new PortRowViewModel(state.Port, state.IsMapped, state.MappingKnown));
            }
        }
    }
}
