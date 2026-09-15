using System.Linq;
using System.Net;
using ValheimServerGUI.Core.Network;
using Xunit;

namespace ValheimServerGUI.Core.Tests.Network
{
    public class ValheimPortsTests
    {
        [Fact]
        public void GetRequiredMappings_ReturnsThreeAdjacentUdpPorts()
        {
            var mappings = ValheimPorts.GetRequiredMappings(2456);

            Assert.Equal(3, mappings.Count);
            Assert.Equal(new[] { 2456, 2457, 2458 }, mappings.Select(m => m.Port).ToArray());
            Assert.All(mappings, m => Assert.Equal(PortMappingProtocol.Udp, m.Protocol));
            Assert.All(mappings, m => Assert.Equal(m.Port, m.ExternalPort));
        }

        [Fact]
        public void GetRequiredMappings_UsesBasePortForOffsetPorts()
        {
            var mappings = ValheimPorts.GetRequiredMappings(3000);

            Assert.Equal(new[] { 3000, 3001, 3002 }, mappings.Select(m => m.Port).ToArray());
        }

        [Theory]
        [InlineData("10.0.0.5")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.255")]
        [InlineData("192.168.1.10")]
        [InlineData("100.64.0.1")]
        [InlineData("127.0.0.1")]
        [InlineData("169.254.1.1")]
        [InlineData("fc00::1")]
        [InlineData("fe80::1")]
        public void IsPrivateAddress_TrueForPrivateRanges(string address)
        {
            Assert.True(ValheimPorts.IsPrivateAddress(address));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("94.158.219.192")]
        [InlineData("172.32.0.1")]
        [InlineData("172.15.0.1")]
        [InlineData("100.128.0.1")]
        [InlineData("2001:4860:4860::8888")]
        public void IsPrivateAddress_FalseForPublicRanges(string address)
        {
            Assert.False(ValheimPorts.IsPrivateAddress(address));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-an-ip")]
        public void IsPrivateAddress_FalseForInvalidInput(string address)
        {
            Assert.False(ValheimPorts.IsPrivateAddress(address));
        }

        [Fact]
        public void IsPrivateAddress_WorksWithIpAddress()
        {
            Assert.True(ValheimPorts.IsPrivateAddress(IPAddress.Parse("192.168.0.1")));
            Assert.False(ValheimPorts.IsPrivateAddress(IPAddress.Parse("1.1.1.1")));
        }
    }
}
