using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ValheimServerGUI.Avalonia.Views;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
using ValheimServerGUI.Infrastructure;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Main shell ViewModel: profile selection, server status, uptime, IP addresses and
    /// invite code (Phase 2 steps 2-4). Hosts the server controls (options + start/stop/restart).
    /// </summary>
    public partial class ShellViewModel : ObservableObject
    {
        private readonly IUserPreferencesProvider UserPrefsProvider;
        private readonly IServerPreferencesProvider ServerPrefsProvider;
        private readonly ValheimServer Server;
        private readonly IIpAddressProvider IpAddressProvider;
        private readonly IPlayerDataRepository PlayerDataProvider;
        private readonly IUserInteraction UserInteraction;
        private readonly ISoftwareUpdateProvider SoftwareUpdateProvider;
        private readonly IPlatformIntegration PlatformIntegration;
        private readonly IStartupArgsProvider StartupArgs;
        private readonly IApplicationLogger Logger;
        private readonly IServiceProvider ServiceProvider;

        private readonly Stopwatch UptimeTimer = new();

        private readonly Queue<decimal> WorldSaveTimes = new();

        public ObservableCollection<string> Profiles { get; } = new();

        public ServerControlsViewModel ServerControls { get; }

        public PlayersViewModel Players { get; }

        public LogsViewModel Logs { get; }

        public ModsViewModel Mods { get; }

        [ObservableProperty]
        private string? _selectedProfile;

        [ObservableProperty]
        private string? _statusText;

        [ObservableProperty]
        private string _uptime = "00:00:00";

        [ObservableProperty]
        private string? _inviteCode;

        [ObservableProperty]
        private string? _externalIpAddress = "Loading...";

        [ObservableProperty]
        private string? _internalIpAddress = "Loading...";

        [ObservableProperty]
        private string _localIpAddress = "127.0.0.1";

        [ObservableProperty]
        private string? _lastWorldSaveText;

        [ObservableProperty]
        private string? _averageWorldSaveText;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _updateStatusText;

        private readonly System.Threading.Timer? _uptimeTimer;
        private readonly System.Threading.Timer? _updateCheckTimer;

        public ShellViewModel(
            IUserPreferencesProvider userPrefsProvider,
            IServerPreferencesProvider serverPrefsProvider,
            ValheimServer server,
            IIpAddressProvider ipAddressProvider,
            IPlayerDataRepository playerDataProvider,
            IUserInteraction userInteraction,
            ISoftwareUpdateProvider softwareUpdateProvider,
            IPlatformIntegration platformIntegration,
            IStartupArgsProvider startupArgs,
            IApplicationLogger logger,
            ServerControlsViewModel serverControls,
            PlayersViewModel players,
            LogsViewModel logs,
            ModsViewModel mods,
            IServiceProvider serviceProvider)
        {
            UserPrefsProvider = userPrefsProvider;
            ServerPrefsProvider = serverPrefsProvider;
            Server = server;
            IpAddressProvider = ipAddressProvider;
            PlayerDataProvider = playerDataProvider;
            UserInteraction = userInteraction;
            SoftwareUpdateProvider = softwareUpdateProvider;
            PlatformIntegration = platformIntegration;
            StartupArgs = startupArgs;
            Logger = logger;
            ServerControls = serverControls;
            Players = players;
            Logs = logs;
            Mods = mods;
            ServiceProvider = serviceProvider;

            Server.StatusChanged += OnServerStatusChanged;
            Server.InviteCodeReady += OnInviteCodeReady;
            Server.WorldSaved += OnWorldSaved;
            IpAddressProvider.ExternalIpChanged += OnExternalIpChanged;
            IpAddressProvider.InternalIpChanged += OnInternalIpChanged;
            SoftwareUpdateProvider.UpdateCheckStarted += OnUpdateCheckStarted;
            SoftwareUpdateProvider.UpdateCheckFinished += OnUpdateCheckFinished;

            _uptimeTimer = new System.Threading.Timer(OnUptimeTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            // The provider self-gates automatic checks to once per AppSettings.UpdateCheckInterval.
            _updateCheckTimer = new System.Threading.Timer(OnUpdateCheckTick, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

            LoadProfiles();
        }

        private void LoadProfiles()
        {
            var prefs = ServerPrefsProvider.LoadPreferences()
                .OrderByDescending(p => p.LastSaved)
                .ToList();

            foreach (var profile in prefs)
            {
                Profiles.Add(profile.ProfileName);
            }

            // Prefer an explicit CLI profile, then the first auto-start profile, then the most recent.
            var startupProfile = StartupArgs?.ServerProfileName;
            var selected = prefs.FirstOrDefault(p => p.ProfileName == startupProfile)
                ?? prefs.FirstOrDefault(p => p.AutoStart)
                ?? prefs.FirstOrDefault();

            StartServerOnLoad = selected?.AutoStart == true;
            SelectedProfile = selected?.ProfileName;
            // OnSelectedProfileChanged fires on assignment and calls SelectProfile.
        }

        private bool StartServerOnLoad;

        partial void OnSelectedProfileChanged(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            SelectProfile(value);
        }

        private void SelectProfile(string profileName)
        {
            ServerControls.LoadProfile(profileName);
            Logger.Information("Selected server profile: {profileName}", profileName);
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                StatusText = Server.Status.ToString();

                var tasks = new[]
                {
                    IpAddressProvider.LoadExternalIpAddressAsync(),
                    IpAddressProvider.LoadInternalIpAddressAsync(),
                    PlayerDataProvider.LoadAsync(),
                    SoftwareUpdateProvider.CheckForUpdatesAsync(false),
                };

                await Task.WhenAll(tasks);

                if (StartServerOnLoad)
                {
                    StartServerOnLoad = false;
                    Logger.Information("Auto-starting server profile {profile}", SelectedProfile);
                    ServerControls.StartCommand.Execute(null);
                }
            }
            catch (Exception e)
            {
                // Startup data (IP lookups, player cache) must never take down the shell.
                Logger.Error(e, "Failed to load initial shell data");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CheckForUpdates()
        {
            _ = SoftwareUpdateProvider.CheckForUpdatesAsync(true);
        }

        private void OnUpdateCheckTick(object? state)
        {
            _ = SoftwareUpdateProvider.CheckForUpdatesAsync(false);
        }

        private void OnUpdateCheckStarted(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() => UpdateStatusText = "Checking for updates...");
        }

        private void OnUpdateCheckFinished(object? sender, SoftwareUpdateEventArgs e)
        {
            Dispatcher.UIThread.Post(() => HandleUpdateCheckFinished(e));
        }

        private void HandleUpdateCheckFinished(SoftwareUpdateEventArgs e)
        {
            string message;
            bool offerDownload = false;

            if (!e.IsSuccessful)
            {
                var exception = e.Exception?.GetPrimaryException();
                UpdateStatusText = "Update check failed";
                message = $"Update check failed: {exception?.Message}";
            }
            else
            {
                var result = AssemblyHelper.CompareVersion(e.LatestVersion);

                if (result > 0)
                {
                    UpdateStatusText = $"Update available ({e.LatestVersion})";
                    message = $"A newer version of ValheimServerGUI is available ({e.LatestVersion}).";
                    offerDownload = true;
                }
                else if (result == 0)
                {
                    UpdateStatusText = $"Up to date ({e.LatestVersion})";
                    message = "You are running the latest version of ValheimServerGUI.";
                }
                else if (result < 0)
                {
                    UpdateStatusText = $"Pre-release build ({AssemblyHelper.GetApplicationVersion()})";
                    message = $"You are running a pre-release build ({AssemblyHelper.GetApplicationVersion()}). " +
                        $"The latest stable version is {e.LatestVersion}.";
                }
                else
                {
                    UpdateStatusText = "Unable to parse version";
                    message = $"Update check failed: unable to parse version ({e.LatestVersion}).";
                }
            }

            if (!e.IsManualCheck) return;

            _ = ShowUpdateResultAsync(message, offerDownload);
        }

        private async Task ShowUpdateResultAsync(string message, bool offerDownload)
        {
            if (!offerDownload)
            {
                UserInteraction.ShowInfo("Check for Updates", message);
                return;
            }

            var goToDownload = await UserInteraction.ConfirmAsync(
                "Check for Updates",
                $"{message}{Environment.NewLine}Would you like to go to the download page?");

            if (goToDownload)
            {
                PlatformIntegration.OpenWebAddress(AppSettings.UrlUpdates);
            }
        }

        [RelayCommand]
        private void ShowPreferences()
        {
            ShowDialog(() => ServiceProvider.GetRequiredService<PreferencesWindow>(), () => ServiceProvider.GetRequiredService<PreferencesViewModel>());
        }

        [RelayCommand]
        private void ShowAbout()
        {
            ShowDialog(() => ServiceProvider.GetRequiredService<AboutWindow>(), () => ServiceProvider.GetRequiredService<AboutViewModel>());
        }

        [RelayCommand]
        private void ShowDiscord()
        {
            ShowDialog(() => ServiceProvider.GetRequiredService<DiscordSettingsWindow>(), () => ServiceProvider.GetRequiredService<DiscordSettingsViewModel>());
        }

        [RelayCommand]
        private void ShowPortForwarding()
        {
            ShowDialog(() => ServiceProvider.GetRequiredService<PortForwardingWindow>(), () => ServiceProvider.GetRequiredService<PortForwardingViewModel>());
        }

        [RelayCommand]
        private void OpenWorldSettings()
        {
            var worldName = ServerControls.IsNewWorld ? ServerControls.NewWorldName : ServerControls.ExistingWorldName;

            if (string.IsNullOrWhiteSpace(worldName))
            {
                UserInteraction.ShowError(
                    "World Settings",
                    ServerControls.IsNewWorld
                        ? "Enter a new world name before changing difficulty settings."
                        : "Select a world before changing difficulty settings.");
                return;
            }

            var viewModel = ServiceProvider.GetRequiredService<WorldSettingsViewModel>();
            viewModel.Load(worldName);

            ShowDialog(() => ServiceProvider.GetRequiredService<WorldSettingsWindow>(), () => viewModel);
        }

        [RelayCommand]
        private async Task NewProfileAsync()
        {
            var name = await PromptForProfileNameAsync();
            if (name == null) return;

            var prefs = new ServerPreferences { ProfileName = name, Name = name };
            ServerPrefsProvider.SavePreferences(prefs);
            ReloadProfiles(name);
        }

        [RelayCommand]
        private void SaveProfile()
        {
            ServerControls.SaveCurrentProfile();
            ReloadProfiles(ServerControls.ProfileName);
        }

        [RelayCommand]
        private async Task SaveProfileAsAsync()
        {
            var startingText = string.IsNullOrWhiteSpace(ServerControls.ProfileName)
                ? "Copy of Profile"
                : $"Copy of {ServerControls.ProfileName}";

            var name = await PromptForProfileNameAsync(startingText);
            if (name == null) return;

            ServerControls.ProfileName = name;
            ServerControls.SaveCurrentProfile();
            ReloadProfiles(name);
        }

        [RelayCommand]
        private async Task RemoveProfileAsync()
        {
            var name = ServerControls.ProfileName;
            if (string.IsNullOrWhiteSpace(name)) return;

            var confirmed = await UserInteraction.ConfirmAsync("Remove Profile", $"Remove server profile '{name}'?");
            if (!confirmed) return;

            ServerPrefsProvider.RemovePreferences(name);
            ReloadProfiles(null);
        }

        [RelayCommand]
        private void OpenSettingsDirectory()
        {
            var directory = System.IO.Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(AppSettings.UserPrefsFilePathV2));
            PlatformIntegration.OpenDirectory(directory);
        }

        [RelayCommand]
        private void OpenManual()
        {
            PlatformIntegration.OpenWebAddress(AppSettings.UrlHelp);
        }

        [RelayCommand]
        private void OpenIssues()
        {
            PlatformIntegration.OpenWebAddress(AppSettings.UrlIssues);
        }

        private Task<string?> PromptForProfileNameAsync(string? startingText = null)
        {
            return UserInteraction.PromptForTextAsync(
                "Server Profile Name",
                "Enter a server profile name:",
                startingText,
                input =>
                {
                    if (string.IsNullOrWhiteSpace(input) || input.Length > 30)
                    {
                        return "Profile name must be 1-30 characters.";
                    }

                    return ServerPrefsProvider.LoadPreferences(input) == null
                        ? null
                        : "A profile with this name already exists.";
                });
        }

        private void ReloadProfiles(string? selectProfile)
        {
            Profiles.Clear();

            foreach (var profile in ServerPrefsProvider.LoadPreferences().OrderByDescending(p => p.LastSaved))
            {
                Profiles.Add(profile.ProfileName);
            }

            SelectedProfile = selectProfile != null && Profiles.Contains(selectProfile)
                ? selectProfile
                : Profiles.FirstOrDefault();
        }

        private void ShowDialog(Func<Window> windowFactory, Func<object> viewModelFactory)
        {
            try
            {
                var window = windowFactory();
                window.DataContext = viewModelFactory();
                window.Opened += (_, _) => Services.WindowsTheme.ApplyDarkTitleBar(window);
                window.ShowDialog(GetOwnerWindow());
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to open dialog window");
            }
        }

        private static Window GetOwnerWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow != null)
            {
                return desktop.MainWindow;
            }

            return new Window();
        }

        private void OnServerStatusChanged(object? sender, ServerStatus status)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = status.ToString();

                if (status == ServerStatus.Running)
                {
                    UptimeTimer.Restart();
                    _uptimeTimer?.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                }
                else
                {
                    if (UptimeTimer.IsRunning) UptimeTimer.Stop();
                    _uptimeTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    Uptime = "00:00:00";

                    if (status == ServerStatus.Stopped)
                    {
                        InviteCode = null;
                    }
                }
            });
        }

        private void OnUptimeTick(object? state)
        {
            Dispatcher.UIThread.Post(() => Uptime = UptimeTimer.Elapsed.ToString(@"hh\:mm\:ss"));
        }

        private void OnInviteCodeReady(object? sender, string inviteCode)
        {
            Dispatcher.UIThread.Post(() => InviteCode = inviteCode);
        }

        private void OnWorldSaved(object? sender, decimal duration)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LastWorldSaveText = $"{DateTime.Now:G} ({duration:F}ms)";

                if (WorldSaveTimes.Count >= 10) WorldSaveTimes.Dequeue();
                WorldSaveTimes.Enqueue(duration);

                AverageWorldSaveText = $"{WorldSaveTimes.Average():F}ms";
            });
        }

        [RelayCommand]
        private Task CopyExternalIpAsync() => UserInteraction.CopyToClipboardAsync(ExternalIpAddress ?? string.Empty);

        [RelayCommand]
        private Task CopyInternalIpAsync() => UserInteraction.CopyToClipboardAsync(InternalIpAddress ?? string.Empty);

        [RelayCommand]
        private Task CopyLocalIpAsync() => UserInteraction.CopyToClipboardAsync(LocalIpAddress ?? string.Empty);

        [RelayCommand]
        private Task CopyInviteCodeAsync() => UserInteraction.CopyToClipboardAsync(InviteCode ?? string.Empty);

        private void OnExternalIpChanged(object? sender, string ip)
        {
            Dispatcher.UIThread.Post(() => ExternalIpAddress = ip);
        }

        private void OnInternalIpChanged(object? sender, string ip)
        {
            Dispatcher.UIThread.Post(() => InternalIpAddress = ip);
        }
    }
}