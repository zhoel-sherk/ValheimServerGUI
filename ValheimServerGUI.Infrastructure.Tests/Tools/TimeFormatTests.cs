using ValheimServerGUI.Tools;
using Xunit;

namespace ValheimServerGUI.Tests.Tools
{
    public class TimeFormatTests
    {
        [Theory]
        [InlineData(60, "1 min")]
        [InlineData(600, "10 min")]
        [InlineData(1800, "30 min")]
        [InlineData(2700, "45 min")]      // not offered by a preset list; must stay reachable
        [InlineData(3600, "1 h")]
        [InlineData(7200, "2 h")]
        [InlineData(5400, "1 h 30 min")]
        [InlineData(9000, "2 h 30 min")]
        [InlineData(43200, "12 h")]
        [InlineData(86400, "24 h")]
        [InlineData(360, "6 min")]
        public void FormatsDurations(int seconds, string expected)
        {
            Assert.Equal(expected, TimeFormat.FormatDuration(seconds));
        }

        [Theory]
        // Sub-minute values have no clean minute form, so they stay in seconds rather than
        // rounding to a misleading "0 min".
        [InlineData(1, "1 s")]
        [InlineData(30, "30 s")]
        [InlineData(59, "59 s")]
        public void KeepsSubMinuteValuesInSeconds(int seconds, string expected)
        {
            Assert.Equal(expected, TimeFormat.FormatDuration(seconds));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void NonPositiveValuesYieldEmpty(int seconds)
        {
            Assert.Equal(string.Empty, TimeFormat.FormatDuration(seconds));
        }

        [Fact]
        public void ExactHourMultiplesDoNotShowMinutes()
        {
            Assert.Equal("1 h", TimeFormat.FormatDuration(3600));
            Assert.Equal("3 h", TimeFormat.FormatDuration(10800));
            Assert.DoesNotContain("min", TimeFormat.FormatDuration(10800));
        }
    }
}