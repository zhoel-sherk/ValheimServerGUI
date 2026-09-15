using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Discord webhook notification settings (ported from upstream PR #83). The webhook URL is
    /// stored locally in userprefs.json and treated like a password.
    /// </summary>
    public partial class DiscordSettingsViewModel : ObservableObject
    {
        private readonly IUserPreferencesProvider UserPrefsProvider;
        private readonly IUserInteraction UserInteraction;
        private readonly IApplicationLogger Logger;

        [ObservableProperty]
        private bool _enableNotifications;

        [ObservableProperty]
        private string? _webhookUrl;

        [ObservableProperty]
        private bool _showWebhook;

        [ObservableProperty]
        private bool _notifyServerOnline;

        [ObservableProperty]
        private bool _notifyServerOffline;

        [ObservableProperty]
        private bool _notifyPlayerJoined;

        [ObservableProperty]
        private bool _notifyPlayerLeft;

        [ObservableProperty]
        private bool _notifyPlayerDied;

        [ObservableProperty]
        private bool _notifyJoinCode;

        [ObservableProperty]
        private bool _isBusy;

        public DiscordSettingsViewModel(
            IUserPreferencesProvider userPrefsProvider,
            IUserInteraction userInteraction,
            IApplicationLogger logger)
        {
            UserPrefsProvider = userPrefsProvider;
            UserInteraction = userInteraction;
            Logger = logger;

            Load();
        }

        private void Load()
        {
            var prefs = UserPrefsProvider.LoadPreferences();

            EnableNotifications = prefs.DiscordStatusNotifications;
            WebhookUrl = prefs.DiscordWebhookUrl ?? string.Empty;
            NotifyServerOnline = prefs.DiscordNotifyServerOnline;
            NotifyServerOffline = prefs.DiscordNotifyServerOffline;
            NotifyPlayerJoined = prefs.DiscordNotifyPlayerJoined;
            NotifyPlayerLeft = prefs.DiscordNotifyPlayerLeft;
            NotifyPlayerDied = prefs.DiscordNotifyPlayerDied;
            NotifyJoinCode = prefs.DiscordNotifyJoinCode;
        }

        [RelayCommand]
        private void Save()
        {
            var prefs = UserPrefsProvider.LoadPreferences();

            prefs.DiscordStatusNotifications = EnableNotifications;
            prefs.DiscordWebhookUrl = WebhookUrl?.Trim();
            prefs.DiscordNotifyServerOnline = NotifyServerOnline;
            prefs.DiscordNotifyServerOffline = NotifyServerOffline;
            prefs.DiscordNotifyPlayerJoined = NotifyPlayerJoined;
            prefs.DiscordNotifyPlayerLeft = NotifyPlayerLeft;
            prefs.DiscordNotifyPlayerDied = NotifyPlayerDied;
            prefs.DiscordNotifyJoinCode = NotifyJoinCode;

            UserPrefsProvider.SavePreferences(prefs);
            Logger.Information("Discord notification settings saved");
        }

        [RelayCommand]
        private async Task TestWebhookAsync()
        {
            if (IsBusy) return;

            var webhookUrl = WebhookUrl?.Trim();
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                UserInteraction.ShowError("Discord Webhook", "Enter a Discord webhook URL first.");
                return;
            }

            IsBusy = true;
            try
            {
                await DiscordWebhookClient.SendAsync(
                    webhookUrl,
                    "✅ **ValheimServerGUI Discord webhook test successful.**");

                UserInteraction.ShowInfo("Discord Webhook", "Test message sent successfully.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Discord webhook test failed");
                UserInteraction.ShowError("Discord Webhook",
                    $"Failed to send the test message.{Environment.NewLine}{Environment.NewLine}{e.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
