using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace ValheimServerGUI.Core.Network
{
    /// <summary>
    /// Valheim-specific port knowledge: the dedicated server uses three adjacent UDP ports
    /// (game, query and Steam networking) starting at the configured base port.
    /// </summary>
    public static class ValheimPorts
    {
        /// <summary>Number of adjacent UDP ports the server needs (base, base+1, base+2).</summary>
        public const int RequiredPortCount = 3;

        /// <summary>Description written to the gateway for mappings created by this app.</summary>
        public const string MappingDescription = "ValheimServerGUI";

        /// <summary>
        /// The UDP mappings required to host on <paramref name="basePort"/>.
        /// </summary>
        public static IReadOnlyList<PortMappingRequest> GetRequiredMappings(int basePort)
        {
            var mappings = new List<PortMappingRequest>(RequiredPortCount);
            for (var offset = 0; offset < RequiredPortCount; offset++)
            {
                mappings.Add(new PortMappingRequest(basePort + offset, PortMappingProtocol.Udp, MappingDescription));
            }

            return mappings;
        }

        /// <summary>
        /// True when the address is loopback, link-local, or in a private/CGNAT range
        /// (10/8, 172.16/12, 192.168/16, 100.64/10, fc00::/7).
        /// </summary>
        public static bool IsPrivateAddress(string ipAddress)
        {
            return !string.IsNullOrWhiteSpace(ipAddress)
                && IPAddress.TryParse(ipAddress, out var address)
                && IsPrivateAddress(address);
        }

        /// <summary>
        /// True when the address is loopback, link-local, or in a private/CGNAT range.
        /// </summary>
        public static bool IsPrivateAddress(IPAddress address)
        {
            if (address == null) return false;
            if (IPAddress.IsLoopback(address)) return true;

            var bytes = address.GetAddressBytes();

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal) return true;

                // Unique local addresses: fc00::/7
                return (bytes[0] & 0xFE) == 0xFC;
            }

            if (bytes.Length != 4) return false;

            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) // CGNAT 100.64/10
                || (bytes[0] == 169 && bytes[1] == 254);                   // link-local
        }
    }
}
