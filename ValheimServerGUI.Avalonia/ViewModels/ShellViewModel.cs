using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ValheimServerGUI.Avalonia.Views;
using ValheimServerGUI.Game;
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
        private readonly IApplicationLogger Logger;
        private readonly IServiceProvider ServiceProvider;

        private readonly Stopwatch UptimeTimer = new();

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
        private bool _isBusy;

        private readonly System.Threading.Timer? _uptimeTimer;

        public ShellViewModel(
            IUserPreferencesProvider userPrefsProvider,
            IServerPreferencesProvider serverPrefsProvider,
            ValheimServer server,
            IIpAddressProvider ipAddressProvider,
            IPlayerDataRepository playerDataProvider,
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
            Logger = logger;
            ServerControls = serverControls;
            Players = players;
            Logs = logs;
            Mods = mods;
            ServiceProvider = serviceProvider;

            Server.StatusChanged += OnServerStatusChanged;
            Server.InviteCodeReady += OnInviteCodeReady;
            IpAddressProvider.ExternalIpChanged += OnExternalIpChanged;
            IpAddressProvider.InternalIpChanged += OnInternalIpChanged;

            _uptimeTimer = new System.Threading.Timer(OnUptimeTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            LoadProfiles();
        }

        private void LoadProfiles()
        {
            foreach (var profile in ServerPrefsProvider.LoadPreferences())
            {
                Profiles.Add(profile.ProfileName);
            }

            SelectedProfile = Profiles.FirstOrDefault();
            // OnSelectedProfileChanged fires on assignment and calls SelectProfile.
        }

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
                };

                await Task.WhenAll(tasks);
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
            // Software update check is wired up in a later step (Phase 2 step 8).
            Logger.Information("Update check not yet implemented in the Avalonia client");
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