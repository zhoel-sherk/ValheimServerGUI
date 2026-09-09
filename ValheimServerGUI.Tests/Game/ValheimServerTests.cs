using System;
using System.IO;
using System.Linq;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Models;
using Xunit;

namespace ValheimServerGUI.Tests.Game
{
    public class ValheimServerTests : BaseTest
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

        private static readonly string TestFolder = Path.Combine(Path.GetTempPath(), "vsg-tests");
        private static readonly string TestServerExe = Path.Combine(TestFolder, "valheim_server_test.exe");
        private static readonly string TestSaveFolder = Path.Combine(TestFolder, "savedata");

        private readonly ValheimServer Server;
        private readonly IPlayerDataRepository PlayerDataRepository;

        static ValheimServerTests()
        {
            Directory.CreateDirectory(TestFolder);
            Directory.CreateDirectory(TestSaveFolder);

            if (!File.Exists(TestServerExe))
            {
                File.WriteAllText(TestServerExe, string.Empty);
            }
        }

        public ValheimServerTests()
        {
            Server = GetService<ValheimServer>();
            //Server.Logger.SetFileLoggingEnabled(false); // todo: fix tests for new logging changes
            PlayerDataRepository = GetService<IPlayerDataRepository>();
        }

        [Fact]
        public void ServerIsStoppedInitially()
        {
            Assert.Equal(ServerStatus.Stopped, Server.Status);
        }

        [Fact]
        public void CanDetectServerRunning()
        {
            ServerStatus? eventStatus = null;
            Server.StatusChanged += (_, status) => eventStatus = status;

            Log("Game server connected");

            Assert.Equal(ServerStatus.Running, Server.Status);
            Assert.Equal(ServerStatus.Running, eventStatus);
        }

        [Fact]
        public void DoesNotDetectServerRunningOnFailure()
        {
            ServerStatus? eventStatus = null;
            Server.StatusChanged += (_, status) => eventStatus = status;

            Log("Game server connected failed");

            Assert.Equal(ServerStatus.Starting, Server.Status);
            Assert.Equal(ServerStatus.Starting, eventStatus);
        }

        [Theory]
        [InlineData("1,024", 1024)]
        [InlineData("1234", 1234)]
        public void CanDetectWorldSaved(string elapsed, decimal expected)
        {
            decimal eventTime = -1;
            Server.WorldSaved += (_, timeMs) => eventTime = timeMs;

            Log(MessageWorldSavedNew, elapsed);

            Assert.Equal(expected, eventTime);
        }

        [Fact]
        public void CanDetectWorldSavedLegacy()
        {
            decimal eventTime = -1;
            Server.WorldSaved += (_, timeMs) => eventTime = timeMs;

            Log(MessageWorldSaved, "823");

            Assert.Equal(823, eventTime);
        }

        [Theory]
        [InlineData(MessageInviteCode)]
        [InlineData(MessageInviteCodeRegistered)]
        public void CanDetectInviteCode(string messageFormat)
        {
            string eventCode = null;
            Server.InviteCodeReady += (_, code) => eventCode = code;

            Log(messageFormat, "424941");

            Assert.Equal("424941", eventCode);
        }

        [Fact]
        public void CanDetectPlayerJoining()
        {
            var playerId = "1234";
            PlayerInfo eventPlayer = null;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            Log(MessageJoining, playerId);

            var expected = new PlayerInfo
            {
                Platform = PlayerPlatforms.Steam,
                PlayerId = playerId,
                PlayerStatus = PlayerStatus.Joining
            };
            AssertMatch(expected, eventPlayer);

            var dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
        }

        [Theory]
        [InlineData(PlayerPlatforms.Steam, "1234")]
        [InlineData(PlayerPlatforms.Xbox, "5678")]
        public void CanDetectPlayerJoiningCrossplay(string platform, string playerId)
        {
            PlayerInfo eventPlayer = null;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            Log(MessageJoiningCrossplay, platform, playerId);

            var expected = new PlayerInfo
            {
                Platform = platform,
                PlayerId = playerId,
                PlayerStatus = PlayerStatus.Joining
            };
            AssertMatch(expected, eventPlayer);

            var dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
        }

        [Fact]
        public void CanDetectPlayerLeaving()
        {
            var playerId = "1234";
            Log(MessageJoining, playerId);
            PlayerInfo eventPlayer = null;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            Log(MessageWrongPassword, playerId);

            var expected = new PlayerInfo
            {
                Platform = PlayerPlatforms.Steam,
                PlayerId = playerId,
                PlayerStatus = PlayerStatus.Leaving
            };
            AssertMatch(expected, eventPlayer);

            var dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
        }

        [Fact]
        public void CanDetectPlayerLeavingVersionMismatch()
        {
            var playerId = "1234";
            Log(MessageJoining, playerId);
            PlayerInfo eventPlayer = null;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            Log(MessageVersionMismatch, playerId);

            var expected = new PlayerInfo
            {
                Platform = PlayerPlatforms.Steam,
                PlayerId = playerId,
                PlayerStatus = PlayerStatus.Leaving
            };
            AssertMatch(expected, eventPlayer);

            var dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
        }

        [Fact]
        public void CanDetectPlayerOffline()
        {
            var playerId = "1234";
            Log(MessageJoining, playerId);
            PlayerInfo eventPlayer = null;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            Log(MessageOffline, playerId);

            var expected = new PlayerInfo
            {
                Platform = PlayerPlatforms.Steam,
                PlayerId = playerId,
                PlayerStatus = PlayerStatus.Offline
            };
            AssertMatch(expected, eventPlayer);

            var dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
        }

        [Theory]
        [InlineData(MessageOfflineCrossplay)]
        [InlineData(MessageOfflineCrossplayNew)]
        public void CanDetectPlayerOfflineCrossplay(string messageFormat)
        {
            var playerId = "5678";
            Log(MessageJoiningCrossplay, PlayerPlatforms.Xbox, playerId);
            PlayerInfo eventPlayer = null;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            Log(messageFormat, playerId);

            var expected = new PlayerInfo
            {
                Platform = PlayerPlatforms.Xbox,
                PlayerId = playerId,
                PlayerStatus = PlayerStatus.Offline
            };
            AssertMatch(expected, eventPlayer);

            var dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
        }

        // If only one player is joining, and that player goes online,
        // then that one player's name and steamId should match up.
        [Fact]
        public void CanMatchPlayerNameSingle()
        {
            // If only one player by steamId is joining...
            var platform = PlayerPlatforms.Steam;
            var playerId = "1234";
            Log(MessageJoining, playerId);

            PlayerInfo eventPlayer = null;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            // And a player by name goes online...
            var (characterName, zdoId) = ("Broheim", "-56789123");
            Log(MessageOnline, characterName, zdoId);

            // ...then that player name and steamId are confidently matched up.
            var expected = CreatePlayer(platform, playerId, characterName, PlayerStatus.Online, zdoId, lastStatusCharacter: characterName);
            AssertMatch(expected, eventPlayer);

            var dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
        }

        // If multiple players are joining at the same time, and no names are known,
        // then nobody in the group will go online until all joining players have connected
        [Fact]
        public void CanHandleMultiplePlayersJoiningAtOnce()
        {
            // If multiple players are joining at the same time...
            var platform = PlayerPlatforms.Steam;
            var ids = new[] { "123", "456", "789" };
            Log(MessageJoining, ids[0]);
            Log(MessageJoining, ids[1]);
            Log(MessageJoining, ids[2]);

            PlayerInfo eventPlayer = null;
            var eventCalls = 0;
            PlayerDataRepository.PlayerStatusChanged += (_, player) =>
            {
                eventCalls++;
                eventPlayer = player;
            };

            Assert.Equal(3, PlayerDataRepository.Data.Count());

            // And a character by name comes online...
            var (name1, zdoid1) = ("CharacterOne", "-432");
            Log(MessageOnline, name1, zdoid1);

            // ...then the earliest joining player is assigned that name with low confidence
            var expected = CreatePlayer(platform, ids[0], name1, PlayerStatus.Online, zdoid1, lastStatusCharacter: name1);
            AssertMatch(expected, eventPlayer);

            // And another character comes online...
            var (name2, zdoid2) = ("CharacterTwo", "5834");
            Log(MessageOnline, name2, zdoid2);

            // ...then the second-earliest joining player is assigned that name with low confidence
            expected = CreatePlayer(platform, ids[1], name2, PlayerStatus.Online, zdoid2, lastStatusCharacter: name2);
            AssertMatch(expected, eventPlayer);

            // And the final player comes online...
            var (name3, zdoid3) = ("CharacterThree", "146131");
            Log(MessageOnline, name3, zdoid3);

            // ...then the last joining player is assigned that name with low confidence
            expected = CreatePlayer(platform, ids[2], name3, PlayerStatus.Online, zdoid3, lastStatusCharacter: name3);
            AssertMatch(expected, eventPlayer);
        }

        // If the same steamId joins under multiple character names, then we can track separate
        // records for each character name
        [Fact]
        public void CanTrackMultipleCharactersForSameSteamId()
        {
            // If a player has joined in the past...
            var platform = PlayerPlatforms.Steam;
            var playerId = "1234";
            var (name1, zdoid1) = ("CharacterOne", "234");
            Log(MessageJoining, playerId);
            Log(MessageOnline, name1, zdoid1);
            Log(MessageOffline, playerId);

            PlayerInfo eventPlayer = null;
            PlayerInfo dataPlayer;
            PlayerInfo expected;
            PlayerDataRepository.PlayerStatusChanged += (_, player) => eventPlayer = player;

            dataPlayer = PlayerDataRepository.Data.Single();
            var origTimestamp = dataPlayer.LastStatusChange; // For later assertion

            // And joins again...
            Log(MessageJoining, playerId);

            // ...then that player is marked as Joining
            expected = CreatePlayer(platform, playerId, name1, PlayerStatus.Joining);
            AssertMatch(expected, eventPlayer);

            dataPlayer = Assert.Single(PlayerDataRepository.Data);
            AssertMatch(expected, dataPlayer);
            var origKey = dataPlayer.Key; // For lookup in later assertion

            // And that player connects under a different character name...
            var (name2, zdoid2) = ("CharacterTwo", "567");
            Log(MessageOnline, name2, zdoid2);

            // ...then a new character is added, and lastStatusCharacter is updated
            expected = CreatePlayer(platform, playerId, name2, PlayerStatus.Online, zdoid2, lastStatusCharacter: name2);
            AssertMatch(expected, eventPlayer);

            Assert.Single(PlayerDataRepository.Data);
            dataPlayer = PlayerDataRepository.FindById(eventPlayer.Key);
            AssertMatch(expected, dataPlayer);
        }

        #region Helper methods

        private void Log(string message, params object[] args)
        {
            if (Server.Logger == null)
            {
                // A new server logger is now instantiated each time the server is started,
                // so let's pretend to boot one up just for testing the log messages.
                // Tests use dummy exe & save data paths so that no real Valheim install is required.
                var options = new ValheimServerOptions
                {
                    Name = "Test Server",
                    Password = "hunter2",
                    WorldName = "Test World",
                    Public = false,
                    Port = 2456,
                    Crossplay = false,
                    SaveInterval = 30,
                    Backups = 1,
                    BackupShort = 60,
                    BackupLong = 120,
                    ServerExePath = TestServerExe,
                    SaveDataFolderPath = TestSaveFolder,
                    LogToFile = false,
                };

                Server.Start(options);
            }

            Server.Logger.Information(string.Format(message, args));
        }

        private static PlayerInfo CreatePlayer(
            string platform,
            string playerId,
            string characterName = null,
            PlayerStatus? status = null,
            string zdoid = null,
            bool confident = false,
            string lastStatusChange = null,
            string lastStatusCharacter = null
        )
        {
            var player = new PlayerInfo
            {
                Platform = platform,
                PlayerId = playerId,
                PlayerStatus = status ?? PlayerStatus.Offline,
                LastStatusChange = lastStatusChange != null ? DateTime.Parse(lastStatusChange) : GetNextTimestamp(),
                LastStatusCharacter = lastStatusCharacter,
                ZdoId = zdoid,
            };

            if (characterName != null)
            {
                player.AddCharacter(characterName, confident);
            }

            return player;
        }

        private static int TimeCounter = 0;

        private static DateTime GetNextTimestamp()
        {
            return DateTime.Parse("01-01-2021").Add(TimeSpan.FromHours(++TimeCounter));
        }

        private static void AssertMatch(PlayerInfo expected, PlayerInfo actual)
        {
            Assert.NotNull(actual);

            Assert.Equal(expected.Platform, actual.Platform);
            Assert.Equal(expected.PlayerId, actual.PlayerId);
            Assert.Equal(expected.PlayerStatus, actual.PlayerStatus);

            if (expected.LastStatusCharacter != null) Assert.Equal(expected.LastStatusCharacter, actual.LastStatusCharacter);
            if (expected.PlayerName != null) Assert.Equal(expected.PlayerName, actual.PlayerName);
            if (expected.ZdoId != null) Assert.Equal(expected.ZdoId, actual.ZdoId);
        }

        #endregion
    }
}
