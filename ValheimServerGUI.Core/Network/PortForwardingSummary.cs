using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimServerGUI.Core.Network
{
    /// <summary>
    /// High-level state of the three UDP ports the server needs, derived from a gateway check.
    /// </summary>
    public enum PortForwardingState
    {
        /// <summary>Nothing has been checked yet.</summary>
        NotChecked,

        /// <summary>No UPnP / NAT-PMP gateway was found on the network.</summary>
        NoGateway,

        /// <summary>A gateway was found, but its mappings could not be read (state unknown).</summary>
        Unknown,

        /// <summary>All required ports are forwarded.</summary>
        AllMapped,

        /// <summary>Some, but not all, required ports are forwarded.</summary>
        PartiallyMapped,

        /// <summary>None of the required ports are forwarded.</summary>
        NoneMapped,
    }

    /// <summary>
    /// Platform-neutral, testable summary of a port-forwarding check: an overall state plus a
    /// human-readable line for the UI. Kept free of any Avalonia/UI types.
    /// </summary>
    public sealed class PortForwardingSummary
    {
        public PortForwardingState State { get; init; }

        public string Text { get; init; } = string.Empty;

        public int MappedCount { get; init; }

        public int TotalCount { get; init; }

        public bool IsAllMapped => State == PortForwardingState.AllMapped;

        public bool HasMapped => MappedCount > 0;

        /// <summary>Initial state before the first check.</summary>
        public static PortForwardingSummary NotChecked { get; } = new()
        {
            State = PortForwardingState.NotChecked,
            Text = "Not checked yet",
        };

        /// <summary>Summary for a failed check (discovery threw before producing a status).</summary>
        public static PortForwardingSummary Error(string message) => new()
        {
            State = PortForwardingState.NoGateway,
            Text = string.IsNullOrWhiteSpace(message) ? "Port check failed" : message,
        };

        /// <summary>Builds the summary from a gateway discovery result.</summary>
        public static PortForwardingSummary From(PortForwardingStatus status)
        {
            if (status == null) return NotChecked;

            if (!status.GatewayFound)
            {
                return new PortForwardingSummary
                {
                    State = PortForwardingState.NoGateway,
                    Text = "No UPnP gateway found",
                };
            }

            var ports = status.Ports ?? Array.Empty<PortMappingState>();
            var total = ports.Count;
            var mapped = ports.Count(p => p.IsMapped);

            if (status.MappingsReadFailed || total == 0)
            {
                return new PortForwardingSummary
                {
                    State = PortForwardingState.Unknown,
                    Text = "Gateway found — could not read the current port mappings",
                    MappedCount = mapped,
                    TotalCount = total,
                };
            }

            return FromCounts(mapped, total);
        }

        /// <summary>Builds the summary from the port states returned by a map/remove operation.</summary>
        public static PortForwardingSummary FromPorts(IReadOnlyList<PortMappingState> ports)
        {
            var list = ports ?? Array.Empty<PortMappingState>();

            if (list.Count > 0 && list.All(p => !p.MappingKnown))
            {
                return new PortForwardingSummary
                {
                    State = PortForwardingState.Unknown,
                    Text = "Could not read the current port mappings",
                    TotalCount = list.Count,
                };
            }

            return FromCounts(list.Count(p => p.IsMapped), list.Count);
        }

        private static PortForwardingSummary FromCounts(int mapped, int total)
        {
            if (total > 0 && mapped == total)
            {
                return new PortForwardingSummary
                {
                    State = PortForwardingState.AllMapped,
                    Text = $"All {total} UDP ports are forwarded",
                    MappedCount = mapped,
                    TotalCount = total,
                };
            }

            if (mapped == 0)
            {
                return new PortForwardingSummary
                {
                    State = PortForwardingState.NoneMapped,
                    Text = $"No UDP ports are forwarded ({total} required)",
                    MappedCount = mapped,
                    TotalCount = total,
                };
            }

            return new PortForwardingSummary
            {
                State = PortForwardingState.PartiallyMapped,
                Text = $"{mapped} of {total} UDP ports are forwarded",
                MappedCount = mapped,
                TotalCount = total,
            };
        }
    }
}
