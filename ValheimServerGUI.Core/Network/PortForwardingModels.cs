using System;
using System.Collections.Generic;

namespace ValheimServerGUI.Core.Network
{
    public enum PortMappingProtocol
    {
        Udp,
        Tcp,
    }

    /// <summary>
    /// A single port mapping to create on the gateway: traffic to <see cref="ExternalPort"/> on the
    /// WAN is forwarded to <see cref="Port"/> on this machine.
    /// </summary>
    public sealed class PortMappingRequest
    {
        public PortMappingRequest(int port, PortMappingProtocol protocol, string description = null, int? externalPort = null)
        {
            Port = port;
            ExternalPort = externalPort ?? port;
            Protocol = protocol;
            Description = description;
        }

        public int Port { get; }

        public int ExternalPort { get; }

        public PortMappingProtocol Protocol { get; }

        public string Description { get; }
    }

    /// <summary>
    /// The observed state of a requested mapping on the gateway.
    /// </summary>
    public sealed class PortMappingState
    {
        public int Port { get; init; }

        public int ExternalPort { get; init; }

        public PortMappingProtocol Protocol { get; init; }

        public bool IsMapped { get; init; }

        /// <summary>
        /// True when <see cref="IsMapped"/> reflects the gateway's real state. False when the
        /// gateway was reached but its mapping list could not be read — the UI then shows an
        /// "unknown" state instead of a misleading "not mapped".
        /// </summary>
        public bool MappingKnown { get; init; } = true;

        public string Description { get; init; }
    }

    /// <summary>
    /// Result of a gateway discovery: whether a UPnP/NAT-PMP device was found, its external address
    /// and the current state of the requested ports. <see cref="Error"/> is set instead of throwing.
    /// </summary>
    public sealed class PortForwardingStatus
    {
        public bool GatewayFound { get; init; }

        public string GatewayEndpoint { get; init; }

        public string ExternalIpAddress { get; init; }

        /// <summary>
        /// True when the external address is in a private/CGNAT range, meaning a port mapping is
        /// unlikely to make the server reachable from the internet.
        /// </summary>
        public bool IsExternalAddressPrivate { get; init; }

        /// <summary>
        /// True when the gateway was reached but its existing mappings could not be read, so the
        /// per-port states are unknown rather than "not mapped".
        /// </summary>
        public bool MappingsReadFailed { get; init; }

        public IReadOnlyList<PortMappingState> Ports { get; init; } = Array.Empty<PortMappingState>();

        public string Error { get; init; }
    }

    /// <summary>
    /// Result of creating or removing mappings.
    /// </summary>
    public sealed class PortMappingResult
    {
        public bool Success { get; init; }

        public string Error { get; init; }

        public IReadOnlyList<PortMappingState> Ports { get; init; } = Array.Empty<PortMappingState>();
    }
}
