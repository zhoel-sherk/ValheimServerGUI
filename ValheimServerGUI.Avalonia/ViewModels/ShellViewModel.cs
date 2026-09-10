using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IApplicationLogger Logger;

        private readonly Stopwatch UptimeTimer = new();

        public ObservableCollection<string> Profiles { get; } = new();

        public ServerControlsViewModel ServerControls { get; }

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
            IApplicationLogger logger,
            ServerControlsViewModel serverControls)
        {
            UserPrefsProvider = userPrefsProvider;
            ServerPrefsProvider = serverPrefsProvider;
            Server = server;
            IpAddressProvider = ipAddressProvider;
            Logger = logger;
            ServerControls = serverControls;

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
            if (SelectedProfile != null)
            {
                SelectProfile(SelectedProfile);
            }
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
                };

                await Task.WhenAll(tasks);
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