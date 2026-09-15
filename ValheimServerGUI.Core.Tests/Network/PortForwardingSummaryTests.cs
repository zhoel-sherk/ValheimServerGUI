using System.Collections.Generic;
using ValheimServerGUI.Core.Network;
using Xunit;

namespace ValheimServerGUI.Core.Tests.Network
{
    public class PortForwardingSummaryTests
    {
        private static PortMappingState State(int port, bool mapped, bool known = true) => new()
        {
            Port = port,
            ExternalPort = port,
            Protocol = PortMappingProtocol.Udp,
            IsMapped = mapped,
            MappingKnown = known,
        };

        private static PortForwardingStatus Status(
            bool gatewayFound,
            bool readFailed,
            params PortMappingState[] ports) => new()
        {
            GatewayFound = gatewayFound,
            MappingsReadFailed = readFailed,
            Ports = ports,
        };

        [Fact]
        public void From_Null_ReturnsNotChecked()
        {
            var summary = PortForwardingSummary.From(null);

            Assert.Equal(PortForwardingState.NotChecked, summary.State);
            Assert.False(summary.IsAllMapped);
        }

        [Fact]
        public void From_NoGateway_ReturnsNoGateway()
        {
            var summary = PortForwardingSummary.From(Status(gatewayFound: false, readFailed: false));

            Assert.Equal(PortForwardingState.NoGateway, summary.State);
        }

        [Fact]
        public void From_ReadFailed_ReturnsUnknownNotNotMapped()
        {
            var summary = PortForwardingSummary.From(Status(
                gatewayFound: true,
                readFailed: true,
                State(2456, mapped: false, known: false),
                State(2457, mapped: false, known: false),
                State(2458, mapped: false, known: false)));

            Assert.Equal(PortForwardingState.Unknown, summary.State);
            Assert.Equal(3, summary.TotalCount);
        }

        [Fact]
        public void From_AllMapped_ReturnsAllMapped()
        {
            var summary = PortForwardingSummary.From(Status(
                gatewayFound: true,
                readFailed: false,
                State(2456, true),
                State(2457, true),
                State(2458, true)));

            Assert.Equal(PortForwardingState.AllMapped, summary.State);
            Assert.True(summary.IsAllMapped);
            Assert.True(summary.HasMapped);
            Assert.Equal(3, summary.MappedCount);
            Assert.Equal(3, summary.TotalCount);
            Assert.Contains("3", summary.Text);
        }

        [Fact]
        public void From_SomeMapped_ReturnsPartiallyMapped()
        {
            var summary = PortForwardingSummary.From(Status(
                gatewayFound: true,
                readFailed: false,
                State(2456, true),
                State(2457, false),
                State(2458, false)));

            Assert.Equal(PortForwardingState.PartiallyMapped, summary.State);
            Assert.Equal(1, summary.MappedCount);
            Assert.Equal(3, summary.TotalCount);
        }

        [Fact]
        public void From_NoneMapped_ReturnsNoneMapped()
        {
            var summary = PortForwardingSummary.From(Status(
                gatewayFound: true,
                readFailed: false,
                State(2456, false),
                State(2457, false),
                State(2458, false)));

            Assert.Equal(PortForwardingState.NoneMapped, summary.State);
            Assert.False(summary.HasMapped);
        }

        [Fact]
        public void FromPorts_AllKnownMapped_ReturnsAllMapped()
        {
            var summary = PortForwardingSummary.FromPorts(new List<PortMappingState>
            {
                State(2456, true),
                State(2457, true),
                State(2458, true),
            });

            Assert.Equal(PortForwardingState.AllMapped, summary.State);
        }

        [Fact]
        public void FromPorts_AllUnknown_ReturnsUnknown()
        {
            var summary = PortForwardingSummary.FromPorts(new List<PortMappingState>
            {
                State(2456, false, known: false),
                State(2457, false, known: false),
                State(2458, false, known: false),
            });

            Assert.Equal(PortForwardingState.Unknown, summary.State);
        }

        [Fact]
        public void Error_ReturnsNoGatewayWithMessage()
        {
            var summary = PortForwardingSummary.Error("boom");

            Assert.Equal(PortForwardingState.NoGateway, summary.State);
            Assert.Equal("boom", summary.Text);
        }
    }
}
