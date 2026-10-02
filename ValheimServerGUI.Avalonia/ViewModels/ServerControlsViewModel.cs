using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Infrastructure;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;
using ValheimServerGUI.Tools.Models;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Server options + start/stop/restart controls (Phase 2 step 3). Mirrors the WinForms
    /// "Server Controls" and "Advanced Controls" tabs: options are editable only while the
    /// server is stopped, and start validates options + port availability first.
    /// </summary>
    public partial class ServerControlsViewModel : ObservableObject
    {
        private static readonly string NL = Environment.NewLine;

        private readonly ValheimServer Server;
        private readonly IUserPreferencesProvider UserPrefsProvider;
        private readonly IServerPreferencesProvider ServerPrefsProvider;
        private readonly IWorldPreferencesProvider WorldPrefsProvider;
        private readonly IIpAddressProvider IpAddressProvider;
        private readonly ISteamCloudWorldProvider SteamCloudWorlds;
        private readonly IUserInteraction UserInteraction;
        private readonly IServerLogStream ServerLogStream;
        private readonly IBepInExManager BepInExManager;
        private readonly IApplicationLogger Logger;

        private bool IsLoadingState;

        public ObservableCollection<string> WorldNames { get; } = new();

        // Server Controls
        [ObservableProperty]
        private string? _profileName;

        [ObservableProperty]
        private string? _serverName;

        [ObservableProperty]
        private int _port;

        [ObservableProperty]
        private string? _password;

        [ObservableProperty]
        private bool _isPublic;

        [ObservableProperty]
        private bool _isCrossplay;

        [ObservableProperty]
        private bool _isNewWorld;

        [ObservableProperty]
        private bool _showPassword;

        [ObservableProperty]
        private string? _newWorldName;

        [ObservableProperty]
        private string? _existingWorldName;

        // Advanced Controls
        [ObservableProperty]
        private string? _serverExePath;

        [ObservableProperty]
        private string? _saveDataFolderPath;

        [ObservableProperty]
        private int _saveInterval;

        /// <summary>
        /// Human-readable equivalent of <see cref="SaveInterval"/>, shown next to the field so the
        /// raw seconds are not the only thing on screen. The server itself still takes seconds.
        /// </summary>
        public string SaveIntervalHint => FormatIntervalHint(SaveInterval);

        [ObservableProperty]
        private int _backupCount;

        /// <summary>Human-readable equivalent of <see cref="BackupShortInterval"/>.</summary>
        public string BackupShortHint => FormatIntervalHint(BackupShortInterval);

        [ObservableProperty]
        private int _backupShortInterval;

        /// <summary>Human-readable equivalent of <see cref="BackupLongInterval"/>.</summary>
        public string BackupLongHint => FormatIntervalHint(BackupLongInterval);

        [ObservableProperty]
        private int _backupLongInterval;

        private static string FormatIntervalHint(int seconds)
        {
            var formatted = TimeFormat.FormatDuration(seconds);
            return string.IsNullOrEmpty(formatted) ? string.Empty : $"≈ {formatted}";
        }

        partial void OnSaveIntervalChanged(int value)
            => OnPropertyChanged(nameof(SaveIntervalHint));

        partial void OnBackupShortIntervalChanged(int value)
            => OnPropertyChanged(nameof(BackupShortHint));

        partial void OnBackupLongIntervalChanged(int value)
            => OnPropertyChanged(nameof(BackupLongHint));

        [ObservableProperty]
        private bool _autoStart;

        [ObservableProperty]
        private string? _additionalArgs;

        [ObservableProperty]
        private bool _writeServerLogsToFile;

        // Derived state
        [ObservableProperty]
        private bool _isBusy;

        public ServerControlsViewModel(
            ValheimServer server,
            IUserPreferencesProvider userPrefsProvider,
            IServerPreferencesProvider serverPrefsProvider,
            IWorldPreferencesProvider worldPrefsProvider,
            IIpAddressProvider ipAddressProvider,
            ISteamCloudWorldProvider steamCloudWorldProvider,
            IUserInteraction userInteraction,
            IServerLogStream serverLogStream,
            IBepInExManager bepInExManager,
            IApplicationLogger logger)
        {
            Server = server;
            UserPrefsProvider = userPrefsProvider;
            ServerPrefsProvider = serverPrefsProvider;
            WorldPrefsProvider = worldPrefsProvider;
            IpAddressProvider = ipAddressProvider;
            SteamCloudWorlds = steamCloudWorldProvider;
            UserInteraction = userInteraction;
            ServerLogStream = serverLogStream;
            BepInExManager = bepInExManager;
            Logger = logger;

            Server.StatusChanged += OnServerStatusChanged;
            Server.WorldSaved += OnWorldSaved;
            Server.InviteCodeReady += OnInviteCodeReady;
            ServerPrefsProvider.PreferencesSaved += OnPreferencesSaved;
        }

        public void LoadProfile(string profileName)
        {
            IsLoadingState = true;
            try
            {
                ProfileName = profileName;
                var prefs = ServerPrefsProvider.LoadPreferences(profileName);

                ServerName = prefs?.Name;
                Port = prefs?.Port ?? int.Parse(AppSettings.DefaultServerPort);
                Password = prefs?.Password;
                IsPublic = prefs?.Public ?? false;
                IsCrossplay = prefs?.Crossplay ?? false;

                SaveInterval = prefs?.SaveInterval ?? int.Parse(AppSettings.DefaultSaveInterval);
                BackupCount = prefs?.BackupCount ?? int.Parse(AppSettings.DefaultBackupCount);
                BackupShortInterval = prefs?.BackupIntervalShort ?? int.Parse(AppSettings.DefaultBackupIntervalShort);
                BackupLongInterval = prefs?.BackupIntervalLong ?? int.Parse(AppSettings.DefaultBackupIntervalLong);
                AutoStart = prefs?.AutoStart ?? false;
                AdditionalArgs = prefs?.AdditionalArgs;
                ServerExePath = prefs?.ServerExePath;
                SaveDataFolderPath = prefs?.SaveDataFolderPath;
                WriteServerLogsToFile = prefs?.WriteServerLogsToFile ?? true;

                RefreshWorldNames();
                SetWorldSelection(prefs?.WorldName);
            }
            finally
            {
                IsLoadingState = false;
            }
        }

        public ValheimServerOptions BuildOptions()
        {
            var userPrefs = UserPrefsProvider.LoadPreferences();
            var worldName = IsNewWorld ? NewWorldName : ExistingWorldName;

            var options = new ValheimServerOptions
            {
                Name = ServerName,
                Password = Password,
                PasswordValidation = userPrefs.EnablePasswordValidation,
                WorldName = worldName,
                Public = IsPublic,
                Port = Port,
                Crossplay = IsCrossplay,
                SaveInterval = SaveInterval,
                Backups = BackupCount,
                BackupShort = BackupShortInterval,
                BackupLong = BackupLongInterval,
                AdditionalArgs = AdditionalArgs,
                ServerExePath = !string.IsNullOrWhiteSpace(ServerExePath) ? ServerExePath : userPrefs.ServerExePath,
                SaveDataFolderPath = !string.IsNullOrWhiteSpace(SaveDataFolderPath) ? SaveDataFolderPath : userPrefs.SaveDataFolderPath,
                LogToFile = WriteServerLogsToFile,
                LogMessageHandler = ServerLogStream.Add,
            };

            var worldNameForPrefs = options.WorldName;
            if (!string.IsNullOrWhiteSpace(worldNameForPrefs))
            {
                var worldPrefs = WorldPrefsProvider.LoadPreferences(worldNameForPrefs);
                if (worldPrefs != null)
                {
                    if (!string.IsNullOrEmpty(worldPrefs.Preset))
                    {
                        options.WorldPreset = worldPrefs.Preset;
                    }
                    else
                    {
                        options.WorldModifiers = worldPrefs.Modifiers;
                    }

                    options.WorldKeys = worldPrefs.Keys;
                }
            }

            return options;
        }

        private void RefreshWorldNames()
        {
            WorldNames.Clear();

            // The save folder may not exist yet (Valheim/server never ran, or a custom path);
            // treat that as "no local worlds" instead of failing to load the profile.
            List<string> localWorlds;
            try
            {
                localWorlds = BuildOptions().GetValidatedSaveDataFolder().GetWorldNames();
            }
            catch (Exception e)
            {
                Logger.Error("Error refreshing world select: {message}", e.Message);
                localWorlds = new List<string>();
            }

            // Also surface Steam Cloud worlds so they can be imported and hosted; local wins on a name collision.
            var cloudWorlds = SteamCloudWorlds.GetCloudWorldNames()
                .Where(n => !localWorlds.Contains(n, StringComparer.OrdinalIgnoreCase))
                .Select(n => n + SteamCloudWorldProvider.CloudWorldSuffix);

            foreach (var worldName in localWorlds.Concat(cloudWorlds))
            {
                WorldNames.Add(worldName);
            }
        }

        private void SetWorldSelection(string? worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName)) return;

            if (WorldNames.Contains(worldName))
            {
                IsNewWorld = false;
                ExistingWorldName = worldName;
            }
            else
            {
                IsNewWorld = true;
                NewWorldName = worldName;
            }
        }

        partial void OnExistingWorldNameChanged(string? value)
        {
            if (!IsLoadingState && value != null)
            {
                IsNewWorld = false;
            }
        }

        [RelayCommand]
        private void RefreshWorlds()
        {
            var selected = IsNewWorld ? NewWorldName : ExistingWorldName;
            RefreshWorldNames();

            if (selected != null && WorldNames.Contains(selected))
            {
                IsNewWorld = false;
                ExistingWorldName = selected;
            }
            else
            {
                IsNewWorld = true;
                NewWorldName = selected;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteStart))]
        private async Task StartAsync()
        {
            if (IsBusy) return;

            // A selected cloud world isn't hostable until its files are brought into the local save
            // folder. Import it here, before options are built, so the suffix never reaches
            // validation or saved prefs.
            if (!IsNewWorld && ExistingWorldName != null &&
                ExistingWorldName.EndsWith(SteamCloudWorldProvider.CloudWorldSuffix, StringComparison.Ordinal))
            {
                var cloudWorldName = ExistingWorldName[..^SteamCloudWorldProvider.CloudWorldSuffix.Length];

                var choice = await UserInteraction.ChooseAsync(
                    "Import cloud world",
                    $"Host the cloud world '{cloudWorldName}'?{NL}{NL}" +
                    "This world is saved to Steam Cloud and must be brought into the server's local " +
                    $"save folder to be hosted.{NL}{NL}" +
                    $"Move: bring the world over and remove the Steam Cloud copy.{NL}" +
                    "Copy: bring a copy over and leave the Steam Cloud copy in place.",
                    new[] { "Move", "Copy", "Cancel" },
                    defaultOption: "Copy");

                if (choice == null || choice == "Cancel") return;

                try
                {
                    var cloudSaveFolder = BuildOptions().GetValidatedSaveDataFolder();
                    SteamCloudWorlds.ImportCloudWorld(cloudWorldName, cloudSaveFolder, move: choice == "Move");
                    Logger.Information("{action} cloud world '{world}' into local save folder",
                        choice == "Move" ? "Moved" : "Copied", cloudWorldName);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import cloud world '{world}'", cloudWorldName);
                    UserInteraction.ShowError("Error starting server", $"Failed to import cloud world '{cloudWorldName}': {e.Message}");
                    return;
                }

                // The world is now local; re-list it without the suffix and select it for the start below
                RefreshWorldNames();
                ExistingWorldName = cloudWorldName;
            }

            var options = BuildOptions();

            try
            {
                options.Validate();
            }
            catch (Exception e)
            {
                UserInteraction.ShowError("Error starting server", e.Message);
                return;
            }

            // Port-in-use detection is best-effort and can report false positives (e.g. a UDP
            // listener bound to another interface). Never block the start on it: log a warning and
            // let the server bind and report its own error if the ports are truly unavailable.
            if (!IpAddressProvider.IsLocalUdpPortAvailable(options.Port, options.Port + 1))
            {
                Logger.Warning(
                    "Port {port} or {nextPort} appears to be in use; starting the server anyway",
                    options.Port, options.Port + 1);
            }

            var worldName = options.WorldName;
            if (IsNewWorld)
            {
                if (string.IsNullOrWhiteSpace(worldName))
                {
                    UserInteraction.ShowError("Error starting server", "You must enter a world name, or choose an existing world.");
                    return;
                }

                if (worldName.Length < 5 || worldName.Length > 20)
                {
                    UserInteraction.ShowError("Error starting server", "World name must be 5-20 characters long.");
                    return;
                }

                if (options.GetValidatedSaveDataFolder().IsWorldNameAvailable(worldName))
                {
                    UserInteraction.ShowError("Error starting server", $"A world named '{worldName}' already exists.");
                    IsNewWorld = false;
                    ExistingWorldName = worldName;
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(worldName) || options.GetValidatedSaveDataFolder().IsWorldNameAvailable(worldName))
                {
                    UserInteraction.ShowError("Error starting server", $"No world exists with name '{worldName}'.");
                    return;
                }
            }

            IsBusy = true;
            try
            {
                // BepInEx's console can hijack the server's stdout, which is the pipe the log
                // parser depends on - the status would then never reach "Running". Repair it on
                // every start, because an install made outside this app never got the fix.
                BepInExManager.EnsureConsoleLoggingDisabled(
                    ValheimPathExtensions.GetServerFolderFromExePath(options.ServerExePath));

                Server.Start(options);

                var userPrefs = UserPrefsProvider.LoadPreferences();
                if (userPrefs.SaveProfileOnStart)
                {
                    SaveCurrentProfile();
                }
            }
            catch (Exception e)
            {
                UserInteraction.ShowError("Error starting server", e.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteStop))]
        private void Stop()
        {
            Server.Stop();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteRestart))]
        private void Restart()
        {
            // Rebuild the options from the current UI/prefs state. Restarting with the snapshot the
            // server was launched with would silently discard any settings changed since then
            // (world difficulty in particular).
            if (!TryBuildValidatedOptions(out var options)) return;

            Server.Restart(options);
        }

        /// <summary>
        /// Builds server options from the current UI state and validates them, reporting any
        /// problem to the user. Shared with callers that need to restart the server with fresh
        /// options, such as applying world settings.
        /// </summary>
        public bool TryBuildValidatedOptions([NotNullWhen(true)] out ValheimServerOptions? options)
        {
            options = BuildOptions();

            try
            {
                options.Validate();
                return true;
            }
            catch (Exception e)
            {
                options = null;
                UserInteraction.ShowError("Error restarting server", e.Message);
                Logger.Error(e, "Invalid server options while restarting");
                return false;
            }
        }

        public void SaveCurrentProfile()
        {
            var prefs = ProfileName == null
                ? new ServerPreferences()
                : ServerPrefsProvider.LoadPreferences(ProfileName) ?? new ServerPreferences { ProfileName = ProfileName };

            prefs.Name = ServerName;
            prefs.Port = Port;
            prefs.Password = Password;
            prefs.WorldName = IsNewWorld ? NewWorldName : ExistingWorldName;
            prefs.Public = IsPublic;
            prefs.Crossplay = IsCrossplay;
            prefs.SaveInterval = SaveInterval;
            prefs.BackupCount = BackupCount;
            prefs.BackupIntervalShort = BackupShortInterval;
            prefs.BackupIntervalLong = BackupLongInterval;
            prefs.AutoStart = AutoStart;
            prefs.AdditionalArgs = AdditionalArgs;
            prefs.ServerExePath = ServerExePath;
            prefs.SaveDataFolderPath = SaveDataFolderPath;
            prefs.WriteServerLogsToFile = WriteServerLogsToFile;

            ServerPrefsProvider.SavePreferences(prefs);
        }

        private void OnServerStatusChanged(object? sender, ServerStatus status)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Once a "new world" starts running, switch back to the existing-worlds list
                // and select the newly created world.
                if (status == ServerStatus.Running && IsNewWorld)
                {
                    var worldName = NewWorldName;
                    RefreshWorldNames();
                    ExistingWorldName = worldName;
                    IsNewWorld = false;
                }

                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(IsServerRunning));
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(CanRestart));
                StartCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
                RestartCommand.NotifyCanExecuteChanged();
            });
        }

        [RelayCommand]
        private async Task CopyPasswordAsync()
        {
            if (!string.IsNullOrWhiteSpace(Password))
            {
                await UserInteraction.CopyToClipboardAsync(Password);
            }
        }

        [RelayCommand]
        private async Task BrowseServerExeAsync()
        {
            var path = await UserInteraction.PickFileAsync("Select valheim_server.exe", "Applications", new[] { ".exe" });
            if (!string.IsNullOrWhiteSpace(path))
            {
                ServerExePath = path;
            }
        }

        [RelayCommand]
        private async Task BrowseSaveFolderAsync()
        {
            var path = await UserInteraction.PickFolderAsync("Select the Valheim save folder");
            if (!string.IsNullOrWhiteSpace(path))
            {
                SaveDataFolderPath = path;
            }
        }

        private void OnWorldSaved(object? sender, decimal duration)
        {
            // Raised on a background thread; no-op here (server details tab handles it).
        }

        private void OnInviteCodeReady(object? sender, string inviteCode)
        {
            // Server details tab handles the invite code.
        }

        private void OnPreferencesSaved(object? sender, System.Collections.Generic.List<ServerPreferences> prefs)
        {
            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(CanEdit)));
        }

        public bool CanEdit => Server.Status == ServerStatus.Stopped;
        public bool CanStart => Server.CanStart && !IsBusy;
        public bool CanStop => Server.CanStop && !IsBusy;
        public bool CanRestart => Server.CanRestart && !IsBusy;

        /// <summary>
        /// True when the server process is up or coming up. Unlike <see cref="CanEdit"/> this stays
        /// true for the whole run, which is what callers need when deciding whether a change has to
        /// restart the server to take effect.
        /// </summary>
        public bool IsServerRunning => Server.IsAnyStatus(ServerStatus.Starting, ServerStatus.Running);

        /// <summary>
        /// True when the named world has already been generated in the save folder. World
        /// difficulty modifiers are baked in at generation time, so this drives a warning when
        /// changing difficulty for a world that already exists.
        /// </summary>
        public bool WorldExists(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName)) return false;

            try
            {
                return !BuildOptions().GetValidatedSaveDataFolder().IsWorldNameAvailable(worldName);
            }
            catch (Exception e)
            {
                // A missing/unreadable save folder just means "can't tell"; don't block the dialog.
                Logger.Error("Error checking whether world '{world}' exists: {message}", worldName, e.Message);
                return false;
            }
        }

        private bool CanExecuteStart() => CanStart;
        private bool CanExecuteStop() => CanStop;
        private bool CanExecuteRestart() => CanRestart;
    }
}