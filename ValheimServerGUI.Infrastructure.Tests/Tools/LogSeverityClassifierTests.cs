using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Logging;
using Xunit;

namespace ValheimServerGUI.Tests.Tools
{
    public class LogSeverityClassifierTests
    {
        // Real server stdout lines, timestamp-prefixed exactly as the UI receives them.
        private const string Noise = "[22:24:36.593] ConnectSpawners => Connected 1719 spawners and 2918 `done` spawners. Removed connection from 4 orphan spawners.";
        private const string NoisePlayerHistory = "[22:24:36.599] Player history entry with index 0: Marine (Steam_76561198046327583, EDC8E28BF62DBE5D)";
        private const string NoiseConnectionStats = "[22:34:41.563] Connections 0 ZDOs:1788063  sent:0 recv:0";

        [Theory]
        // Successful connections / arrivals.
        [InlineData("[22:24:40.501] Game server connected")]
        [InlineData("[00:10:25.124] Got connection SteamID 76561198046327583")]
        [InlineData("[22:24:40.404] PlayFab socket with remote ID playfab/1234 received local Platform ID Steam_76561198046327583")]
        [InlineData("[22:24:36.599] Got character ZDOID from Marine : 76561198046327583:12345")]
        public void ServerLine_SuccessIsHighlightedAsSuccess(string line)
        {
            Assert.Equal(LogSeverity.Success, LogSeverityClassifier.ClassifyServerLine(line));
        }

        [Theory]
        // A death must never read as a fresh login, even though the connected pattern also matches.
        [InlineData("[22:24:36.599] Got character ZDOID from Marine : 0:0")]
        [InlineData("[22:24:36.599] Got character ZDOID from SomeVeryLongPlayerName : 0:0")]
        public void ServerLine_DeathIsNotTreatedAsAJoin(string line)
        {
            Assert.Equal(LogSeverity.Warning, LogSeverityClassifier.ClassifyServerLine(line));
        }

        [Theory]
        [InlineData("[22:24:36.599] Peer 12345 has wrong password")]
        [InlineData("[22:24:36.599] Peer 12345 has incompatible version, mine:1.0.7 (network version 39)   remote 1.0.6 (network version 38)")]
        public void ServerLine_ConnectionRefusedIsError(string line)
        {
            Assert.Equal(LogSeverity.Error, LogSeverityClassifier.ClassifyServerLine(line));
        }

        [Theory]
        [InlineData("[22:24:40.404] Session \"Astrahan\" with join code ABC123 and IP 1.2.3.4:2456 is active with 1 player(s)")]
        [InlineData("[22:24:40.404] Session \"Astrahan\" registered with join code ABC123")]
        [InlineData("[22:24:20.000] World save (5/5) done. Total time [1,234ms]")]
        [InlineData("[22:24:20.000] World saved (1234 ms)")]
        public void ServerLine_NoteworthyButNotAnEventIsInfo(string line)
        {
            Assert.Equal(LogSeverity.Info, LogSeverityClassifier.ClassifyServerLine(line));
        }

        [Fact]
        public void ServerLine_StartupFailureIsNotHighlightedAsSuccess()
        {
            // The pattern must not match "Game server connected failed".
            Assert.Equal(LogSeverity.Normal, LogSeverityClassifier.ClassifyServerLine("[22:24:40.501] Game server connected failed"));
        }

        [Theory]
        [InlineData(Noise)]
        [InlineData(NoisePlayerHistory)]
        [InlineData(NoiseConnectionStats)]
        [InlineData("[22:24:36.424] ConnectPortals => Connected 28 portals.")]
        [InlineData("[22:24:36.599] Destroying abandoned non persistent zdo 76561198046327583:12345 owner 4194304")]
        [InlineData("[22:24:36.599] Closing socket 12345")]
        public void ServerLine_ChatterStaysNormal(string line)
        {
            Assert.Equal(LogSeverity.Normal, LogSeverityClassifier.ClassifyServerLine(line));
        }

        [Theory]
        [InlineData("[22:24:40.501] [ERR] Server run command: boom")]
        [InlineData("[22:24:40.501] [FAT] Unhandled exception")]
        public void ApplicationLine_ErrorsAreHighlighted(string line)
        {
            Assert.Equal(LogSeverity.Error, LogSeverityClassifier.ClassifyApplicationLine(line));
        }

        [Fact]
        public void ApplicationLine_WarningIsHighlighted()
        {
            Assert.Equal(
                LogSeverity.Warning,
                LogSeverityClassifier.ClassifyApplicationLine("[02:28:26.673] [WRN] Crossplay is enabled and BepInEx appears to be installed"));
        }

        [Theory]
        [InlineData("[22:24:36.599] Server saved world")]
        [InlineData("[22:24:36.599] [DBG] HTTP request was successful (200)")]
        [InlineData("[22:24:36.599] [VER] Starting up")]
        public void ApplicationLine_InfoAndDebugStayNormal(string line)
        {
            Assert.Equal(LogSeverity.Normal, LogSeverityClassifier.ClassifyApplicationLine(line));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyInputIsNormal(string line)
        {
            Assert.Equal(LogSeverity.Normal, LogSeverityClassifier.ClassifyServerLine(line));
            Assert.Equal(LogSeverity.Normal, LogSeverityClassifier.ClassifyApplicationLine(line));
        }

        [Fact]
        public void ApplicationLine_LevelIsNotMatchedInTheTimestamp()
        {
            // Guards against a naive "contains [ERR]" check: the timestamp itself has brackets.
            Assert.Equal(
                LogSeverity.Error,
                LogSeverityClassifier.ClassifyApplicationLine("[02:28:26.673] [ERR] Player error"));
        }
    }
}