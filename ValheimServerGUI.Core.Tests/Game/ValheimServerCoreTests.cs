using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Logging;
using ValheimServerGUI.Core.Processes;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Models;
using Xunit;

#pragma warning disable CS0067 // Events on test fakes are never raised
namespace ValheimServerGUI.Core.Tests.Game
{
    public class ValheimServerCoreTests : IDisposable
    {
        private static readonly string TestFolder = Path.Combine(Path.GetTempPath(), "vsg-core-tests", "server", Guid.NewGuid().ToString("N"));
        private static readonly string TestServerExe = Path.Combine(TestFolder, "valheim_server.exe");
        private static readonly string TestSaveFolder = Path.Combine(TestFolder, "savedata");

        public ValheimServerCoreTests()
        {
            Directory.CreateDirectory(TestFolder);
            Directory.CreateDirectory(TestSaveFolder);
            File.WriteAllText(TestServerExe, string.Empty);
        }

        public void Dispose()
        {
            try { Directory.Delete(TestFolder, recursive: true); } catch { }
        }

        private ValheimServerOptions GetOptions()
        {
            return new ValheimServerOptions
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
        }

        private (ValheimServer Server, MockCoreProcessFactory ProcessFactory, MockPlayerRepo Repo) CreateServer()
        {
            var processFactory = new MockCoreProcessFactory();
            var repo = new MockPlayerRepo();
            var server = new ValheimServer(processFactory, repo, new MockAppLog(), new MockServerLoggerFactory());
            return (server, processFactory, repo);
        }

        private void StartServer(ValheimServer server)
        {
            server.Start(GetOptions());
        }

        [Fact]
        public void StartTransitionStartingAndBuildsArgs()
        {
            var (server, factory, _) = CreateServer();

            StartServer(server);
            var process = factory.Last;

            Assert.Equal(ServerStatus.Starting, server.Status);
            Assert.True(process.IsStarted);
            Assert.Contains("-name \"Test Server\"", process.Spec.Arguments);
            Assert.Contains("-world \"Test World\"", process.Spec.Arguments);
            Assert.Equal("892970", process.Spec.EnvironmentVariables["SteamAppId"]);
        }

        [Fact]
        public void ServerConnectedLineTransitionsToRunning()
        {
            var (server, factory, _) = CreateServer();
            StartServer(server);

            factory.Last.SimulateOutput("Game server connected");

            Assert.Equal(ServerStatus.Running, server.Status);
        }

        [Fact]
        public void ServerConnectedFailedDoesNotTransition()
        {
            var (server, factory, _) = CreateServer();
            StartServer(server);

            factory.Last.SimulateOutput("Game server connected failed");

            Assert.Equal(ServerStatus.Starting, server.Status);
        }

        [Fact]
        public void PlayerConnectingUpdatesPlayerRepo()
        {
            var (server, factory, repo) = CreateServer();
            StartServer(server);

            factory.Last.SimulateOutput("Got connection SteamID 1234");

            Assert.Contains("1234", repo.JoiningSteamIds);
        }

        [Fact]
        public void PlayerConnectedUpdatesPlayerRepo()
        {
            var (server, factory, repo) = CreateServer();
            StartServer(server);

            factory.Last.SimulateOutput("Got character ZDOID from Broheim : -56789123:1");

            Assert.Equal("Broheim", repo.OnlineCharacter);
            Assert.Equal("-56789123", repo.OnlineZdoId);
        }

        [Fact]
        public void PlayerDeathRaisesPlayerDiedAndDoesNotLogIn()
        {
            var (server, factory, repo) = CreateServer();
            StartServer(server);

            string died = null;
            server.PlayerDied += (_, name) => died = name;

            factory.Last.SimulateOutput("Got character ZDOID from Broheim : 0:0");

            Assert.Equal("Broheim", died);
            Assert.Null(repo.OnlineCharacter);
        }

        [Fact]
        public void PlayerStatusChangesRecordServerName()
        {
            var (server, factory, repo) = CreateServer();
            StartServer(server);

            factory.Last.SimulateOutput("Got character ZDOID from Broheim : -56789123:1");

            Assert.Equal("Test Server", repo.LastServerName);
        }

        [Fact]
        public void StopTerminatesProcess()
        {
            var (server, factory, _) = CreateServer();
            StartServer(server);
            var process = factory.Last;
            Assert.False(process.IsStopped);

            server.Stop();

            Assert.True(process.IsStopped);
            Assert.Equal(ServerStatus.Stopping, server.Status);
        }

        [Fact]
        public void ExitedEventResetsToStopped()
        {
            var (server, factory, _) = CreateServer();
            StartServer(server);

            factory.Last.SimulateExit();

            Assert.Equal(ServerStatus.Stopped, server.Status);
            Assert.True(server.CanStart);
        }
    }

    public class MockCoreProcess : IServerProcess
    {
        public ServerProcessSpec Spec { get; }

        public bool IsStarted { get; private set; }

        public bool IsStopped { get; private set; }

        public MockCoreProcess(ServerProcessSpec spec)
        {
            Spec = spec;
        }

        public event Action<string> OutputDataReceived;
        public event Action<string> ErrorDataReceived;
        public event Action Exited;

        public void Start() => IsStarted = true;

        public void Stop() => IsStopped = true;

        public void SimulateOutput(string data) => OutputDataReceived?.Invoke(data);

        public void SimulateError(string data) => ErrorDataReceived?.Invoke(data);

        public void SimulateExit() => Exited?.Invoke();

        public void Dispose() { }
    }

    public class MockCoreProcessFactory : IServerProcessFactory
    {
        public MockCoreProcess Last { get; private set; }

        public IServerProcess Create(ServerProcessSpec spec)
        {
            Last = new MockCoreProcess(spec);
            return Last;
        }
    }

    public class MockAppLog : IApplicationLog
    {
        public void Information(string messageTemplate, params object[] propertyValues) { }
        public void Warning(string messageTemplate, params object[] propertyValues) { }
        public void Error(string messageTemplate, params object[] propertyValues) { }
        public void Error(Exception exception, string messageTemplate, params object[] propertyValues) { }
    }

    public class MockServerLogger : IServerLogger
    {
        public event Action<string> LogReceived;
        public IEnumerable<string> LogBuffer => Array.Empty<string>();
        public void Information(string message) => LogReceived?.Invoke(message);
        public void Error(string message) => LogReceived?.Invoke(message);
    }

    public class MockServerLoggerFactory : IServerLoggerFactory
    {
        public IServerLogger Create(IValheimServerOptions options) => new MockServerLogger();
    }

    public class MockPlayerRepo : IPlayerDataRepository
    {
        public List<string> JoiningSteamIds { get; } = new();
        public string OnlineCharacter { get; private set; }
        public string OnlineZdoId { get; private set; }
        public string LastServerName { get; private set; }

        public event EventHandler DataReady;
        public event EventHandler DataUpdated;
        public event EventHandler DataCleared;
        public event EventHandler<PlayerInfo> EntityUpdated;
        public event EventHandler<PlayerInfo> EntityRemoved;
        public event EventHandler<PlayerInfo> PlayerStatusChanged;

        public IEnumerable<PlayerInfo> Data => Array.Empty<PlayerInfo>();

        public PlayerInfo FindById(string id) => null;

        public IEnumerable<PlayerInfo> FindPlayersByQuery(PlayerDataQuery query) => Array.Empty<PlayerInfo>();

        public PlayerInfo SetPlayerJoining(string serverName, PlayerDataQuery query)
        {
            LastServerName = serverName;
            if (!string.IsNullOrWhiteSpace(query.PlayerId)) JoiningSteamIds.Add(query.PlayerId);
            return null;
        }

        public PlayerInfo SetPlayerOnline(string serverName, string characterName, string zdoId)
        {
            LastServerName = serverName;
            OnlineCharacter = characterName;
            OnlineZdoId = zdoId;
            return null;
        }

        public void SetPlayerLeaving(string serverName, PlayerDataQuery query) { LastServerName = serverName; }
        public void SetPlayerOffline(string serverName, PlayerDataQuery query) { LastServerName = serverName; }
        public void Upsert(PlayerInfo entity) { }
        public void UpsertBulk(IEnumerable<PlayerInfo> entities) { }
        public void Remove(string key) { }
        public void Remove(PlayerInfo entity) { }
        public void RemoveBulk(IEnumerable<string> keys) { }
        public void RemoveBulk(IEnumerable<PlayerInfo> entities) { }
        public void RemoveAll() { }
        public Task LoadAsync() => Task.CompletedTask;
    }
}
#pragma warning restore CS0067