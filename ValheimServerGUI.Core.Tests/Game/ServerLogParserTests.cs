using System;
using System.Collections.Generic;
using ValheimServerGUI.Game;
using Xunit;

namespace ValheimServerGUI.Core.Tests.Game
{
    public class ServerLogParserTests
    {
        private const string MessageJoining = "Got connection SteamID {0}";
        private const string MessageJoiningCrossplay = "PlayFab socket with remote ID 123456 received local Platform ID {0}_{1}";
        private const string MessageOnline = "Got character ZDOID from {0} : {1}:1";
        private const string MessageWrongPassword = "Peer {0} has wrong password";
        private const string MessageVersionMismatch = "Peer {0} has incompatible version, mine:1.0.7 (network version 39)   remote 1.0.6 (network version 38)";
        private const string MessageOffline = "Closing socket {0}";
        private const string MessageOfflineCrossplay = "Destroying abandoned non persistent zdo {0}:1";
        private const string MessageOfflineCrossplayNew = "Destroying abandoned non persistent zdo {0}:1 owner 4194304";
        private const string MessageWorldSaved = "World saved ({0} ms)";
        private const string MessageWorldSavedNew = "World save (5/5) done. Total time [{0}ms]";
        private const string MessageInviteCode = "Session \"Test Server\" with join code {0} and IP 1.2.3.4:2456 is active with 1 player(s)";
        private const string MessageInviteCodeRegistered = "Session \"Test Server\" registered with join code {0}";

        private static ServerLogParser CreateParser(out Dictionary<string, string[]> captures)
        {
            var parser = new ServerLogParser();
            var captured = new Dictionary<string, string[]>();
            foreach (var (name, pattern) in Patterns())
            {
                parser.AddAction(pattern, c => captured[name] = c);
            }
            captures = captured;
            return parser;
        }

        private static IEnumerable<(string Name, string Pattern)> Patterns()
        {
            yield return ("ServerConnected", ServerLogPatterns.ServerConnected);
            yield return ("WorldSavedLegacy", ServerLogPatterns.WorldSavedLegacy);
            yield return ("WorldSaved", ServerLogPatterns.WorldSaved);
            yield return ("CrossplayJoinCodeActive", ServerLogPatterns.CrossplayJoinCodeActive);
            yield return ("CrossplayJoinCodeRegistered", ServerLogPatterns.CrossplayJoinCodeRegistered);
            yield return ("PlayerConnecting", ServerLogPatterns.PlayerConnecting);
            yield return ("PlayerConnectingCrossplay", ServerLogPatterns.PlayerConnectingCrossplay);
            yield return ("PlayerConnected", ServerLogPatterns.PlayerConnected);
            yield return ("PlayerDisconnectingWrongPassword", ServerLogPatterns.PlayerDisconnectingWrongPassword);
            yield return ("PlayerDisconnectingIncompatibleVersion", ServerLogPatterns.PlayerDisconnectingIncompatibleVersion);
            yield return ("PlayerDisconnected", ServerLogPatterns.PlayerDisconnected);
            yield return ("PlayerDisconnectedCrossplay", ServerLogPatterns.PlayerDisconnectedCrossplay);
        }

        [Fact]
        public void DetectsServerRunning()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage("[00:00:01.000] Game server connected");
            Assert.Contains("ServerConnected", captures.Keys);
        }

        [Fact]
        public void DoesNotDetectServerRunningOnFailure()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage("[00:00:01.000] Game server connected failed");
            Assert.DoesNotContain("ServerConnected", captures.Keys);
        }

        [Theory]
        [InlineData("1,024", "1,024")]
        [InlineData("1234", "1234")]
        public void DetectsWorldSaved(string elapsed, string expected)
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageWorldSavedNew, elapsed)}");
            Assert.Contains("WorldSaved", captures.Keys);
            Assert.Equal(expected, captures["WorldSaved"][0]);
        }

        [Fact]
        public void DetectsWorldSavedLegacy()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageWorldSaved, "823")}");
            Assert.Contains("WorldSavedLegacy", captures.Keys);
            Assert.Equal("823", captures["WorldSavedLegacy"][0]);
        }

        [Theory]
        [InlineData(MessageInviteCode, "CrossplayJoinCodeActive")]
        [InlineData(MessageInviteCodeRegistered, "CrossplayJoinCodeRegistered")]
        public void DetectsInviteCode(string messageFormat, string expectedKey)
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(messageFormat, "424941")}");
            Assert.Contains(expectedKey, captures.Keys);
            Assert.Equal("424941", captures[expectedKey][0]);
        }

        [Fact]
        public void DetectsPlayerConnecting()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageJoining, "1234")}");
            Assert.Contains("PlayerConnecting", captures.Keys);
            Assert.Equal("1234", captures["PlayerConnecting"][0]);
        }

        [Theory]
        [InlineData("Steam", "1234")]
        [InlineData("Xbox", "5678")]
        public void DetectsPlayerConnectingCrossplay(string platform, string playerId)
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageJoiningCrossplay, platform, playerId)}");
            Assert.Contains("PlayerConnectingCrossplay", captures.Keys);
            Assert.Equal(new[] { platform, playerId }, captures["PlayerConnectingCrossplay"]);
        }

        [Fact]
        public void DetectsPlayerConnected()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageOnline, "Broheim", "-56789123")}");
            Assert.Contains("PlayerConnected", captures.Keys);
            Assert.Equal("Broheim", captures["PlayerConnected"][0]);
        }

        [Fact]
        public void DetectsPlayerDisconnectingWrongPassword()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageWrongPassword, "1234")}");
            Assert.Contains("PlayerDisconnectingWrongPassword", captures.Keys);
        }

        [Fact]
        public void DetectsPlayerDisconnectingIncompatibleVersion()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageVersionMismatch, "1234")}");
            Assert.Contains("PlayerDisconnectingIncompatibleVersion", captures.Keys);
        }

        [Fact]
        public void DetectsPlayerDisconnected()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(MessageOffline, "1234")}");
            Assert.Contains("PlayerDisconnected", captures.Keys);
        }

        [Theory]
        [InlineData(MessageOfflineCrossplay)]
        [InlineData(MessageOfflineCrossplayNew)]
        public void DetectsPlayerDisconnectedCrossplay(string messageFormat)
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage($"[00:00:01.000] {string.Format(messageFormat, "5678")}");
            Assert.Contains("PlayerDisconnectedCrossplay", captures.Keys);
        }

        [Fact]
        public void NoActionMatchesRandomNoise()
        {
            var parser = CreateParser(out var captures);
            parser.HandleMessage("[00:00:01.000] Unloading 6 unused Assets to reduce memory usage. Loaded Objects now: 1.");
            Assert.Empty(captures);
        }

        [Fact]
        public void ErrorInHandlerIsReportedButParsingContinues()
        {
            var parser = new ServerLogParser();
            var errors = new List<string>();
            parser.AddAction(ServerLogPatterns.ServerConnected, _ => throw new InvalidOperationException("boom"));
            parser.AddAction(ServerLogPatterns.PlayerConnecting, c => { });

            parser.HandleMessage("[00:00:01.000] Game server connected", (e, m) => errors.Add(m));
            parser.HandleMessage("[00:00:01.000] Got connection SteamID 1234", (e, m) => errors.Add(m));

            Assert.Single(errors);
        }
    }
}