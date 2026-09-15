using Mono.Nat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Network;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Infrastructure.Network
{
    /// <summary>
    /// UPnP IGD / NAT-PMP port forwarder built on Mono.Nat. Discovery is event-based (SSDP);
    /// the first device found is cached for the session. No method throws: failures are returned
    /// in the result models so the UI can show them.
    /// </summary>
    public sealed class UpnpPortForwarder : IPortForwarder
    {
        private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(5);

        private readonly IApplicationLogger Logger;
        private readonly SemaphoreSlim DiscoverLock = new(1, 1);

        private INatDevice Device;

        public UpnpPortForwarder(IApplicationLogger logger)
        {
            Logger = logger;
        }

        public async Task<PortForwardingStatus> DiscoverAsync(IReadOnlyList<PortMappingRequest> ports, CancellationToken cancellationToken = default)
        {
            try
            {
                var device = await GetDeviceAsync(cancellationToken).ConfigureAwait(false);
                if (device == null)
                {
                    return new PortForwardingStatus
                    {
                        GatewayFound = false,
                        Error = "No UPnP or NAT-PMP gateway was found on the network.",
                        Ports = EmptyStates(ports),
                    };
                }

                var externalIp = await TryGetExternalIpAsync(device).ConfigureAwait(false);
                var mappings = await TryGetAllMappingsAsync(device).ConfigureAwait(false);

                return new PortForwardingStatus
                {
                    GatewayFound = true,
                    GatewayEndpoint = device.DeviceEndpoint?.ToString(),
                    ExternalIpAddress = externalIp,
                    IsExternalAddressPrivate = ValheimPorts.IsPrivateAddress(externalIp),
                    Ports = ports.Select(p => BuildState(p, mappings)).ToList(),
                };
            }
            catch (Exception e)
            {
                Logger.Error(e, "UPnP discovery failed");
                return new PortForwardingStatus { GatewayFound = false, Error = e.Message, Ports = EmptyStates(ports) };
            }
        }

        public Task<PortMappingResult> MapAsync(IReadOnlyList<PortMappingRequest> ports, CancellationToken cancellationToken = default)
        {
            return ApplyAsync(ports, create: true, cancellationToken);
        }

        public Task<PortMappingResult> RemoveAsync(IReadOnlyList<PortMappingRequest> ports, CancellationToken cancellationToken = default)
        {
            return ApplyAsync(ports, create: false, cancellationToken);
        }

        #region Implementation

        private async Task<PortMappingResult> ApplyAsync(IReadOnlyList<PortMappingRequest> ports, bool create, CancellationToken cancellationToken)
        {
            try
            {
                var device = await GetDeviceAsync(cancellationToken).ConfigureAwait(false);
                if (device == null)
                {
                    return new PortMappingResult
                    {
                        Success = false,
                        Error = "No UPnP or NAT-PMP gateway was found on the network.",
                        Ports = EmptyStates(ports),
                    };
                }

                var errors = new List<string>();

                foreach (var request in ports)
                {
                    try
                    {
                        var mapping = ToMapping(request);
                        if (create)
                        {
                            await device.CreatePortMapAsync(mapping).ConfigureAwait(false);
                            Logger.Information("UPnP: mapped {protocol} {port}", request.Protocol, request.Port);
                        }
                        else
                        {
                            await device.DeletePortMapAsync(mapping).ConfigureAwait(false);
                            Logger.Information("UPnP: removed {protocol} {port}", request.Protocol, request.Port);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "UPnP: failed to {action} {protocol} {port}",
                            create ? "map" : "remove", request.Protocol, request.Port);
                        errors.Add($"{request.Protocol} {request.Port}: {e.Message}");
                    }
                }

                var mappings = await TryGetAllMappingsAsync(device).ConfigureAwait(false);

                return new PortMappingResult
                {
                    Success = errors.Count == 0,
                    Error = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors),
                    Ports = ports.Select(p => BuildState(p, mappings)).ToList(),
                };
            }
            catch (Exception e)
            {
                Logger.Error(e, "UPnP operation failed");
                return new PortMappingResult { Success = false, Error = e.Message, Ports = EmptyStates(ports) };
            }
        }

        private async Task<INatDevice> GetDeviceAsync(CancellationToken cancellationToken)
        {
            if (Device != null) return Device;

            await DiscoverLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Device != null) return Device;

                var tcs = new TaskCompletionSource<INatDevice>(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnDeviceFound(object sender, DeviceEventArgs e) => tcs.TrySetResult(e.Device);

                NatUtility.DeviceFound += OnDeviceFound;
                try
                {
                    NatUtility.StartDiscovery();

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(DiscoveryTimeout);
                    using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
                    {
                        try
                        {
                            Device = await tcs.Task.ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            Device = null;
                        }
                    }
                }
                finally
                {
                    NatUtility.DeviceFound -= OnDeviceFound;
                    NatUtility.StopDiscovery();
                }

                return Device;
            }
            finally
            {
                DiscoverLock.Release();
            }
        }

        private async Task<string> TryGetExternalIpAsync(INatDevice device)
        {
            try
            {
                var address = await device.GetExternalIPAsync().ConfigureAwait(false);
                return address?.ToString();
            }
            catch (Exception e)
            {
                Logger.Warning("UPnP: could not read the external IP address: {message}", e.Message);
                return null;
            }
        }

        private async Task<IReadOnlyList<Mapping>> TryGetAllMappingsAsync(INatDevice device)
        {
            try
            {
                return await device.GetAllMappingsAsync().ConfigureAwait(false) ?? Array.Empty<Mapping>();
            }
            catch (Exception e)
            {
                Logger.Warning("UPnP: could not read the existing mappings: {message}", e.Message);
                return Array.Empty<Mapping>();
            }
        }

        private static PortMappingState BuildState(PortMappingRequest request, IReadOnlyList<Mapping> mappings)
        {
            var protocol = ToProtocol(request.Protocol);
            var mapped = mappings?.FirstOrDefault(m => m.Protocol == protocol && m.PublicPort == request.ExternalPort);

            return new PortMappingState
            {
                Port = request.Port,
                ExternalPort = request.ExternalPort,
                Protocol = request.Protocol,
                IsMapped = mapped != null,
                Description = mapped?.Description,
            };
        }

        private static Mapping ToMapping(PortMappingRequest request)
        {
            // Lifetime 0 = protocol default (UPnP: indefinite).
            return new Mapping(ToProtocol(request.Protocol), request.Port, request.ExternalPort, 0, request.Description);
        }

        private static Protocol ToProtocol(PortMappingProtocol protocol)
        {
            return protocol == PortMappingProtocol.Tcp ? Protocol.Tcp : Protocol.Udp;
        }

        private static IReadOnlyList<PortMappingState> EmptyStates(IReadOnlyList<PortMappingRequest> ports)
        {
            return ports.Select(p => new PortMappingState
            {
                Port = p.Port,
                ExternalPort = p.ExternalPort,
                Protocol = p.Protocol,
                IsMapped = false,
            }).ToList();
        }

        #endregion
    }
}
