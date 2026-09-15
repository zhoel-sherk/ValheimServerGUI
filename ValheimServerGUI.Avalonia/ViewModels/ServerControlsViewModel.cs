using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
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

        [ObservableProperty]
        private int _backupCount;

        [ObservableProperty]
        private int _backupShortInterval;

        [ObservableProperty]
        private int _backupLongInterval;

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
            IApplicationLogger logger)
        {
            Server = server;
            UserPrefsProvider = userPrefsProvider;
            ServerPrefsProvider = serverPrefsProvider;
            WorldPrefsProvider = worldPrefsProvider;
            IpAddressProvider = ipAddressProvider;
            SteamCloudWorlds = steamCloudWorldProvider;
            UserInteraction = userInteraction;
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
            var saveFolder = BuildOptions().GetValidatedSaveDataFolder();
            var localWorlds = saveFolder.GetWorldNames();

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

            if (!IpAddressProvider.IsLocalUdpPortAvailable(options.Port, options.Port + 1))
            {
                UserInteraction.ShowError(
                    "Error starting server",
                    $"Port {options.Port} or {options.Port + 1} is already in use.{NL}" +
                    "Valheim requires two adjacent ports to run a dedicated server.{NL}" +
                    "Please shut down any UDP applications using these ports, or choose a different port for your server.");
                return;
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
            Server.Restart();
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
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(CanRestart));
                StartCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
                RestartCommand.NotifyCanExecuteChanged();
            });
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

        private bool CanExecuteStart() => CanStart;
        private bool CanExecuteStop() => CanStop;
        private bool CanExecuteRestart() => CanRestart;
    }
}