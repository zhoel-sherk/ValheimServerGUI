using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Main shell ViewModel: profile selection and server status. This is the Phase 2 smoke-test
    /// surface; server controls (start/stop/restart) are wired up next.
    /// </summary>
    public partial class ShellViewModel : ObservableObject
    {
        private readonly IUserPreferencesProvider UserPrefsProvider;
        private readonly IServerPreferencesProvider ServerPrefsProvider;
        private readonly ValheimServer Server;
        private readonly IApplicationLogger Logger;

        public ObservableCollection<string> Profiles { get; } = new();

        [ObservableProperty]
        private string? _selectedProfile;

        [ObservableProperty]
        private string? _serverStatus;

        [ObservableProperty]
        private bool _isBusy;

        public ShellViewModel(
            IUserPreferencesProvider userPrefsProvider,
            IServerPreferencesProvider serverPrefsProvider,
            ValheimServer server,
            IApplicationLogger logger)
        {
            UserPrefsProvider = userPrefsProvider;
            ServerPrefsProvider = serverPrefsProvider;
            Server = server;
            Logger = logger;

            Server.StatusChanged += OnServerStatusChanged;

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
            var prefs = ServerPrefsProvider.LoadPreferences(profileName);
            if (prefs == null) return;

            Logger.Information("Selected server profile: {profileName}", profileName);
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                Logger.Information("Refreshing server status");
                await Task.Delay(250); // placeholder for an actual refresh
                ServerStatus = Server.Status.ToString();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnServerStatusChanged(object? sender, ServerStatus status)
        {
            // ServerStatus is raised from a background thread; marshal to the UI thread.
            Dispatcher.UIThread.Post(() => ServerStatus = status.ToString());
        }
    }
}