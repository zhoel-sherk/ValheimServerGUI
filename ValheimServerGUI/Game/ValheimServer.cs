using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Processes;
using ValheimServerGUI.Properties;
using ValheimServerGUI.Tools.Logging;
using ValheimServerGUI.Tools.Models;

namespace ValheimServerGUI.Game
{
    public class ValheimServer : IDisposable
    {
        /// <summary>
        /// Options for the currently running server.
        /// </summary>        
        public IValheimServerOptions Options { get; private set; } = new ValheimServerOptions();

        /// <summary>
        /// Exposed for testing.
        /// </summary>
        public IValheimServerLogger Logger => ServerLogger;

        public ServerStatus Status
        {
            get => _status;
            private set
            {
                if (_status == value) return;
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }
        private ServerStatus _status = ServerStatus.Stopped;
        private IServerProcess ServerProcess;
        private bool IsRestarting;
        private readonly ServerLogParser LogParser = new();

        public event EventHandler<ServerStatus> StatusChanged;
        public event EventHandler<decimal> WorldSaved;
        public event EventHandler<string> InviteCodeReady;

        public bool CanStart => IsAnyStatus(ServerStatus.Stopped) && ServerProcess == null;
        public bool CanStop => IsAnyStatus(ServerStatus.Starting, ServerStatus.Running) && ServerProcess != null;
        public bool CanRestart => IsAnyStatus(ServerStatus.Running) && ServerProcess != null;

        private readonly IServerProcessFactory ProcessFactory;
        private readonly IPlayerDataRepository PlayerDataRepository;
        private readonly IApplicationLogger ApplicationLogger;

        /// <summary>
        /// This logger is instantiated each time a new server is started.
        /// </summary>
        private IValheimServerLogger ServerLogger;

        public ValheimServer(
            IServerProcessFactory processFactory,
            IPlayerDataRepository playerDataRepository,
            IApplicationLogger appLogger)
        {
            ProcessFactory = processFactory;
            PlayerDataRepository = playerDataRepository;
            ApplicationLogger = appLogger;

            InitializeLogBasedActions();
            InitializeStatusBasedActions();
        }

        #region Initialization

        private void InitializeLogBasedActions()
        {
            LogParser.AddAction(ServerLogPatterns.ServerConnected, OnServerConnected);
            LogParser.AddAction(ServerLogPatterns.WorldSavedLegacy, OnWorldSaved);
            LogParser.AddAction(ServerLogPatterns.WorldSaved, OnWorldSaved);
            LogParser.AddAction(ServerLogPatterns.CrossplayJoinCodeActive, OnCrossplayJoinCodeAvailable);
            LogParser.AddAction(ServerLogPatterns.CrossplayJoinCodeRegistered, OnCrossplayJoinCodeAvailable);
            LogParser.AddAction(ServerLogPatterns.PlayerConnecting, OnPlayerConnecting);
            LogParser.AddAction(ServerLogPatterns.PlayerConnectingCrossplay, OnPlayerConnectingCrossplay);
            LogParser.AddAction(ServerLogPatterns.PlayerConnected, OnPlayerConnected);
            LogParser.AddAction(ServerLogPatterns.PlayerDisconnectingWrongPassword, OnPlayerDisconnecting);
            LogParser.AddAction(ServerLogPatterns.PlayerDisconnectingIncompatibleVersion, OnPlayerDisconnecting);
            LogParser.AddAction(ServerLogPatterns.PlayerDisconnected, OnPlayerDisconnected);
            LogParser.AddAction(ServerLogPatterns.PlayerDisconnectedCrossplay, OnPlayerDisconnected);
        }

        private void InitializeStatusBasedActions()
        {
            StatusChanged += BuildStatusHandler(ServerStatus.Stopped, () =>
            {
                if (IsRestarting)
                {
                    // There are no more server events to listen to after it has stopped, so
                    // we're just going to artifically delay here to allow any shutdown actions to finish
                    Task.Run(async () =>
                    {
                        await Task.Delay(500);

                        if (!IsRestarting) return;

                        IsRestarting = false;
                        Start(Options);
                    });
                }
            });
        }

        private static EventHandler<ServerStatus> BuildStatusHandler(ServerStatus status, Action action)
        {
            return (obj, s) =>
            {
                if (s == status) action();
            };
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Starts the Valheim server as a background process with the provided options.
        /// </summary>
        public void Start(IValheimServerOptions options)
        {
            if (!CanStart) return;

            ApplicationLogger.Information("Starting server: {name}", options.Name);

            var exePath = options.GetValidatedServerExe().FullName;
            var processArgs = GenerateArgs(options);

            WarnIfModsAndCrossplay(options, exePath);

            ApplicationLogger.Information(
                @"Server run command: ""{exePath}"" {processArgs}",
                exePath,
                CleanArgsForLogging(processArgs));

            var spec = new ServerProcessSpec
            {
                ExecutablePath = exePath,
                Arguments = processArgs,
                WorkingDirectory = Path.GetDirectoryName(exePath),
            };
            spec.EnvironmentVariables["SteamAppId"] = Resources.ValheimSteamAppId;

            var process = ProcessFactory.Create(spec);
            process.OutputDataReceived += Process_OnDataReceived;
            process.ErrorDataReceived += Process_OnErrorReceived;
            process.Exited += OnServerProcessExited;

            ServerLogger = new ValheimServerLogger(options);
            ServerLogger.LogReceived += Logger_OnServerLogReceived;
            if (options.LogMessageHandler != null)
            {
                // Use this to pass messages to a UI component in the MainWindow
                ServerLogger.LogReceived += options.LogMessageHandler;
            }

            ServerProcess = process;
            process.Start();

            Options = options;
            IsRestarting = false;
            Status = ServerStatus.Starting;
        }

        /// <summary>
        /// Gracefully stops the Valheim server process.
        /// </summary>
        public void Stop()
        {
            if (!CanStop) return;

            ApplicationLogger.Information("Stopping server: {name}", Options.Name);

            ServerProcess.Stop();

            IsRestarting = false;
            Status = ServerStatus.Stopping;
        }

        /// <summary>
        /// Gracefully stops the Valheim server process, then starts it up again.
        /// If no options are provided, then the existing server options will be used.
        /// </summary>
        public void Restart(IValheimServerOptions options = null)
        {
            if (!CanRestart) return;

            ApplicationLogger.Information("Restarting server: {name}", Options.Name);

            ServerProcess.Stop();

            Options = options ?? Options;
            IsRestarting = true;
            Status = ServerStatus.Stopping;
        }

        public bool IsAnyStatus(params ServerStatus[] statuses)
        {
            return statuses.Any(s => s == Status);
        }

        #endregion

        #region Event handlers

        private void Process_OnDataReceived(string data)
        {
            ServerLogger.Information(data);
        }

        private void Process_OnErrorReceived(string data)
        {
            ServerLogger.Error(data);
        }

        private void OnServerProcessExited()
        {
            ServerProcess = null;
            Status = ServerStatus.Stopped;
        }

        private void Logger_OnServerLogReceived(string message)
        {
            LogParser.HandleMessage(message, (e, msg) =>
                ApplicationLogger.Error(e, "Error parsing server log: {message}", msg));
        }

        #endregion

        #region Log Message handlers

        private void OnServerConnected(params string[] captures)
        {
            // The server can reach a running state if you attempt to stop it late in the 
            // startup process, so avoid changing status from "Stopping" -> "Running".
            // It will still stop after it fully starts up.
            if (Status == ServerStatus.Stopping) return;

            Status = ServerStatus.Running;
        }

        private void OnPlayerConnecting(params string[] captures)
        {
            var steamId = captures[0];
            if (string.IsNullOrWhiteSpace(steamId)) return;

            PlayerDataRepository.SetPlayerJoining(new() { Platform = PlayerPlatforms.Steam, PlayerId = steamId });
        }

        private void OnPlayerConnectingCrossplay(params string[] captures)
        {
            var hasValidPlatform = PlayerPlatforms.TryGetValidPlatform(captures[0], out var platform);
            var playerId = captures[1];
            if (!hasValidPlatform || string.IsNullOrWhiteSpace(playerId)) return;

            PlayerDataRepository.SetPlayerJoining(new() { Platform = platform, PlayerId = playerId });
        }

        private void OnPlayerConnected(params string[] captures)
        {
            var playerName = captures[0];
            var zdoid = captures[1]; // Seems to be a unique object id for the game session
            //var otherNumber = captures[2]; // Not sure what this is for?

            if (string.IsNullOrWhiteSpace(playerName)) return;

            PlayerDataRepository.SetPlayerOnline(playerName, zdoid);
        }

        private void OnPlayerDisconnecting(params string[] captures)
        {
            var playerIdOrZdoId = captures[0];
            if (string.IsNullOrWhiteSpace(playerIdOrZdoId)) return;

            var query = new PlayerDataQuery
            {
                PlayerId = playerIdOrZdoId,
                Or = new()
                {
                    ZdoId = playerIdOrZdoId,
                }
            };

            PlayerDataRepository.SetPlayerLeaving(query);
        }

        private void OnPlayerDisconnected(params string[] captures)
        {
            var playerIdOrZdoId = captures[0];
            if (string.IsNullOrWhiteSpace(playerIdOrZdoId)) return;

            var query = new PlayerDataQuery
            {
                PlayerId = playerIdOrZdoId,
                Or = new()
                {
                    ZdoId = playerIdOrZdoId,
                }
            };

            PlayerDataRepository.SetPlayerOffline(query);
        }

        private void OnWorldSaved(params string[] captures)
        {
            // Newer server builds format the elapsed time with group separators,
            // which cannot be parsed as a number (e.g. "1,234")
            var timeText = new string(captures[0].Where(char.IsDigit).ToArray());

            if (!decimal.TryParse(timeText, out var timeMs))
            {
                timeMs = 0;
            }

            WorldSaved?.Invoke(this, timeMs);
        }

        private void OnCrossplayJoinCodeAvailable(params string[] captures)
        {
            var inviteCode = captures[0];
            if (string.IsNullOrWhiteSpace(inviteCode)) return;

            InviteCodeReady?.Invoke(this, inviteCode);
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            Stop();

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Helper methods

        private void WarnIfModsAndCrossplay(IValheimServerOptions options, string exePath)
        {
            if (!options.Crossplay) return;

            try
            {
                var serverFolder = Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(serverFolder)) return;

                // winhttp.dll is the BepInEx/doorstop proxy; its presence means mods are installed.
                if (File.Exists(Path.Join(serverFolder, "winhttp.dll")))
                {
                    ApplicationLogger.Warning(
                        "Crossplay is enabled and BepInEx appears to be installed (winhttp.dll found in the server folder). Many mods are incompatible with -crossplay; if players cannot connect, disable crossplay or remove the mods.");
                }
            }
            catch
            {
                // Best-effort detection; never block startup on it
            }
        }

        private static string GenerateArgs(IValheimServerOptions options)
        {
            var saveDataFolder = options.GetValidatedSaveDataFolder().FullName;
            var publicFlag = options.Public ? 1 : 0;
            var processArgs = @$"-nographics -batchmode -name ""{options.Name}"" -port {options.Port} -world ""{options.WorldName}"" -public {publicFlag} -savedir ""{saveDataFolder}"" -saveinterval {options.SaveInterval} -backups {options.Backups} -backupshort {options.BackupShort} -backuplong {options.BackupLong}";

            if (!string.IsNullOrWhiteSpace(options.Password))
            {
                processArgs += @$" -password ""{options.Password}""";
            }

            if (options.Crossplay)
            {
                processArgs += " -crossplay";
            }

            if (!string.IsNullOrWhiteSpace(options.WorldPreset))
            {
                processArgs += $" -preset {options.WorldPreset}";
            }
            else if (options.WorldModifiers != null)
            {
                foreach (var (key, value) in options.WorldModifiers)
                {
                    processArgs += $" -modifier {key} {value}";
                }
            }

            if (options.WorldKeys != null)
            {
                foreach (var key in options.WorldKeys)
                {
                    processArgs += $" -setkey {key}";
                }
            }

            if (!string.IsNullOrWhiteSpace(options.AdditionalArgs))
            {
                processArgs += $" {options.AdditionalArgs}";
            }

            return processArgs;
        }

        private static string CleanArgsForLogging(string processArgs)
        {
            // Don't print server password to logs
            processArgs = Regex.Replace(processArgs, @"-password ""(.*?)""", @"-password ""*****""");

            return processArgs;
        }

        #endregion
    }
}
