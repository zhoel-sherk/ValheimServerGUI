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
    /// One UDP port row in the port-forwarding dialog.
    /// </summary>
    public partial class PortRowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isMapped;

        public PortRowViewModel(int port, bool isMapped)
        {
            Port = port;
            IsMapped = isMapped;
        }

        public int Port { get; }

        public string Protocol => "UDP";

        public string StatusText => IsMapped ? "Mapped" : "Not mapped";

        partial void OnIsMappedChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>
    /// Port forwarding dialog (UPnP / NAT-PMP): discovers the gateway and maps/removes the three
    /// adjacent UDP ports the Valheim server needs. Manual only — nothing is changed automatically.
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
        private string? _statusText = "Not checked yet";

        [ObservableProperty]
        private string? _gatewayText;

        [ObservableProperty]
        private string? _externalIpText;

        [ObservableProperty]
        private bool _showPrivateAddressWarning;

        [ObservableProperty]
        private string? _errorMessage;

        public int BasePort => ServerControls.Port;

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

        [RelayCommand]
        private async Task DiscoverAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            StatusText = "Searching for a gateway...";

            try
            {
                var status = await PortForwarder.DiscoverAsync(GetRequests());

                if (!status.GatewayFound)
                {
                    StatusText = "No UPnP gateway found";
                    GatewayText = null;
                    ExternalIpText = null;
                    ShowPrivateAddressWarning = false;
                    ErrorMessage = status.Error ?? "Make sure UPnP is enabled on your router.";
                    ResetPorts();
                    return;
                }

                StatusText = "Gateway found";
                GatewayText = status.GatewayEndpoint;
                ExternalIpText = string.IsNullOrWhiteSpace(status.ExternalIpAddress) ? "Unknown" : status.ExternalIpAddress;
                ShowPrivateAddressWarning = status.IsExternalAddressPrivate;
                ApplyStates(status.Ports);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Port forwarding discovery failed");
                StatusText = "Discovery failed";
                ErrorMessage = e.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MapAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var result = await PortForwarder.MapAsync(GetRequests());
                ApplyStates(result.Ports);
                ErrorMessage = result.Success ? null : result.Error;
                StatusText = result.Success ? "Ports mapped" : "Some ports could not be mapped";
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

        [RelayCommand]
        private async Task RemoveAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var result = await PortForwarder.RemoveAsync(GetRequests());
                ApplyStates(result.Ports);
                ErrorMessage = result.Success ? null : result.Error;
                StatusText = result.Success ? "Ports removed" : "Some ports could not be removed";
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

        private IReadOnlyList<PortMappingRequest> GetRequests() => ValheimPorts.GetRequiredMappings(BasePort);

        private void ResetPorts()
        {
            ApplyStates(GetRequests().Select(r => new PortMappingState
            {
                Port = r.Port,
                ExternalPort = r.ExternalPort,
                Protocol = r.Protocol,
                IsMapped = false,
            }).ToList());
        }

        private void ApplyStates(IReadOnlyList<PortMappingState> states)
        {
            Ports.Clear();

            if (states == null || states.Count == 0)
            {
                foreach (var request in GetRequests())
                {
                    Ports.Add(new PortRowViewModel(request.Port, false));
                }

                return;
            }

            foreach (var state in states.OrderBy(s => s.Port))
            {
                Ports.Add(new PortRowViewModel(state.Port, state.IsMapped));
            }
        }
    }
}
