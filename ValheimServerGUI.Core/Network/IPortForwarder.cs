using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ValheimServerGUI.Core.Network
{
    /// <summary>
    /// Platform-neutral port-forwarding contract (UPnP IGD / NAT-PMP). Implementations must never
    /// throw: discovery and mapping failures are reported through <see cref="PortForwardingStatus.Error"/>
    /// and <see cref="PortMappingResult.Error"/> so the UI can present them.
    /// </summary>
    public interface IPortForwarder
    {
        /// <summary>
        /// Finds the gateway on the LAN and reports its external address plus the current state of
        /// the requested ports.
        /// </summary>
        Task<PortForwardingStatus> DiscoverAsync(IReadOnlyList<PortMappingRequest> ports, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the requested mappings on the gateway.
        /// </summary>
        Task<PortMappingResult> MapAsync(IReadOnlyList<PortMappingRequest> ports, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the requested mappings from the gateway.
        /// </summary>
        Task<PortMappingResult> RemoveAsync(IReadOnlyList<PortMappingRequest> ports, CancellationToken cancellationToken = default);
    }
}
