using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.Services
{
    /// <summary>
    /// Sends optional Discord webhook notifications for server and player events (ported from
    /// upstream PR #83 to the Avalonia client). Reacts to the platform-neutral
    /// <see cref="ValheimServer"/> / <see cref="IPlayerDataRepository"/> events, so it holds no UI
    /// references and can be reused by any host.
    /// </summary>
    public sealed class DiscordStatusService : IDisposable
    {
        private const int DiscordColorOnline = 0x57F287;
        private const int DiscordColorOffline = 0xED4245;
        private const int DiscordColorPlayerJoin = 0x5865F2;
        private const int DiscordColorPlayerLeave = 0xFEE75C;
        private const int DiscordColorJoinCode = 0xEB459E;

        private readonly ValheimServer Server;
        private readonly IPlayerDataRepository PlayerDataProvider;
        private readonly IUserPreferencesProvider UserPrefsProvider;
        private readonly IIpAddressProvider IpAddressProvider;
        private readonly IApplicationLogger Logger;

        private readonly object SyncRoot = new();
        private readonly Stopwatch UptimeTimer = new();
        private readonly HashSet<string> OnlinePlayerKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTimeOffset> PlayerJoinTimes = new(StringComparer.Ordinal);

        private ServerStatus? LastFinalStatus;
        private string? CurrentJoinCode;
        private bool OnlineAnnouncementSent;
        private bool PlayerNotificationsEnabledForSession;
        private int SessionGeneration;

        public DiscordStatusService(
            ValheimServer server,
            IPlayerDataRepository playerDataProvider,
            IUserPreferencesProvider userPrefsProvider,
            IIpAddressProvider ipAddressProvider,
            IApplicationLogger logger)
        {
            Server = server;
            PlayerDataProvider = playerDataProvider;
            UserPrefsProvider = userPrefsProvider;
            IpAddressProvider = ipAddressProvider;
            Logger = logger;

            LastFinalStatus = Server.Status;

            Server.StatusChanged += OnServerStatusChanged;
            Server.InviteCodeReady += OnInviteCodeReady;
            Server.PlayerDied += OnPlayerDied;
            PlayerDataProvider.PlayerStatusChanged += OnPlayerStatusChanged;
        }

        #region Event handlers

        private void OnServerStatusChanged(object? sender, ServerStatus status)
        {
            if (status == ServerStatus.Running)
            {
                UptimeTimer.Restart();
            }
            else
            {
                UptimeTimer.Stop();
            }

            if (status == ServerStatus.Starting)
            {
                lock (SyncRoot)
                {
                    SessionGeneration++;
                    CurrentJoinCode = null;
                    OnlineAnnouncementSent = false;
                    PlayerNotificationsEnabledForSession = false;
                    OnlinePlayerKeys.Clear();
                    PlayerJoinTimes.Clear();
                }
                return;
            }

            if (status == ServerStatus.Stopping)
            {
                // Avoid a flood of "player left" messages caused by normal server shutdown.
                lock (SyncRoot) { PlayerNotificationsEnabledForSession = false; }
                return;
            }

            if (status == ServerStatus.Running)
            {
                int generation;
                lock (SyncRoot)
                {
                    if (LastFinalStatus == ServerStatus.Running) return;

                    LastFinalStatus = ServerStatus.Running;
                    PlayerNotificationsEnabledForSession = true;
                    generation = SessionGeneration;
                }

                if (Server.Options?.Crossplay == true)
                {
                    // Give PlayFab a few seconds to provide the join code so the main
                    // "online" message can include it.
                    _ = SendServerOnlineAfterJoinCodeGracePeriodAsync(generation);
                }
                else
                {
                    _ = SendServerOnlineAsync(generation);
                }
                return;
            }

            if (status == ServerStatus.Stopped)
            {
                bool wasRunning;
                lock (SyncRoot)
                {
                    PlayerNotificationsEnabledForSession = false;
                    SessionGeneration++;
                    wasRunning = LastFinalStatus == ServerStatus.Running;
                    LastFinalStatus = ServerStatus.Stopped;
                    CurrentJoinCode = null;
                    OnlineAnnouncementSent = false;
                    OnlinePlayerKeys.Clear();
                    PlayerJoinTimes.Clear();
                }

                if (wasRunning)
                {
                    _ = SendServerOfflineAsync();
                }
            }
        }

        private void OnInviteCodeReady(object? sender, string inviteCode)
        {
            if (string.IsNullOrWhiteSpace(inviteCode)) return;

            string? previousCode;
            bool announcementSent;
            int generation;

            lock (SyncRoot)
            {
                previousCode = CurrentJoinCode;
                CurrentJoinCode = inviteCode.Trim();
                announcementSent = OnlineAnnouncementSent;
                generation = SessionGeneration;
            }

            if (Server.Status != ServerStatus.Running) return;

            if (!announcementSent)
            {
                _ = SendServerOnlineAsync(generation);
                return;
            }

            if (!string.Equals(previousCode, CurrentJoinCode, StringComparison.Ordinal))
            {
                _ = SendJoinCodeReadyAsync();
            }
        }

        private void OnPlayerDied(object? sender, string playerName)
        {
            lock (SyncRoot)
            {
                if (!PlayerNotificationsEnabledForSession) return;
            }

            if (Server.Status != ServerStatus.Running || string.IsNullOrWhiteSpace(playerName)) return;

            _ = SendPlayerDiedAsync(playerName.Trim());
        }

        private void OnPlayerStatusChanged(object? sender, PlayerInfo player)
        {
            if (player == null) return;

            lock (SyncRoot)
            {
                if (!PlayerNotificationsEnabledForSession) return;
            }

            if (Server.Status != ServerStatus.Running) return;

            var key = GetPlayerKey(player);

            if (player.PlayerStatus == PlayerStatus.Online)
            {
                lock (SyncRoot)
                {
                    if (!OnlinePlayerKeys.Add(key)) return;
                    PlayerJoinTimes[key] = DateTimeOffset.UtcNow;
                }

                _ = SendPlayerJoinedAsync(player);
                return;
            }

            if (player.PlayerStatus == PlayerStatus.Offline)
            {
                DateTimeOffset joinedAt;

                lock (SyncRoot)
                {
                    // Only announce a leave if we actually saw this player become Online during this
                    // server session. This prevents wrong-password attempts and stale player records
                    // from creating false leave messages.
                    if (!OnlinePlayerKeys.Remove(key)) return;

                    PlayerJoinTimes.TryGetValue(key, out joinedAt);
                    PlayerJoinTimes.Remove(key);
                }

                _ = SendPlayerLeftAsync(player, joinedAt);
            }
        }

        #endregion

        #region Senders

        private async Task SendServerOnlineAfterJoinCodeGracePeriodAsync(int generation)
        {
            await Task.Delay(TimeSpan.FromSeconds(4));

            lock (SyncRoot)
            {
                if (generation != SessionGeneration || OnlineAnnouncementSent) return;
            }

            if (Server.Status != ServerStatus.Running) return;

            await SendServerOnlineAsync(generation);
        }

        private async Task SendServerOnlineAsync(int generation)
        {
            var prefs = UserPrefsProvider.LoadPreferences();
            int playerCount;

            lock (SyncRoot)
            {
                if (generation != SessionGeneration || OnlineAnnouncementSent) return;
                if (Server.Status != ServerStatus.Running) return;

                if (!prefs.DiscordStatusNotifications ||
                    !prefs.DiscordNotifyServerOnline ||
                    string.IsNullOrWhiteSpace(prefs.DiscordWebhookUrl))
                {
                    return;
                }

                OnlineAnnouncementSent = true;
                playerCount = OnlinePlayerKeys.Count;
            }

            try
            {
                var crossplay = Server.Options?.Crossplay == true;
                var joinCode = crossplay
                    ? (string.IsNullOrWhiteSpace(CurrentJoinCode) ? "Loading..." : CurrentJoinCode)
                    : "N/A";

                await DiscordWebhookClient.SendEmbedAsync(
                    prefs.DiscordWebhookUrl,
                    "🟢 Valheim Server Online",
                    $"**{GetServerName()}** is ready.",
                    DiscordColorOnline,
                    ("World", GetWorldName(), true),
                    ("Address", GetConnectionAddress(), true),
                    ("Join Code", joinCode, true),
                    ("Crossplay", crossplay ? "Enabled" : "Disabled", true),
                    ("Players", playerCount.ToString(), true),
                    ("Uptime", FormatDuration(UptimeTimer.Elapsed), true));

                Logger.Information("Discord server online notification sent");
            }
            catch (Exception e)
            {
                lock (SyncRoot) { OnlineAnnouncementSent = false; }
                Logger.Error(e, "Failed to send Discord server online notification");
            }
        }

        private async Task SendServerOfflineAsync()
        {
            var prefs = UserPrefsProvider.LoadPreferences();

            if (!prefs.DiscordStatusNotifications ||
                !prefs.DiscordNotifyServerOffline ||
                string.IsNullOrWhiteSpace(prefs.DiscordWebhookUrl))
            {
                return;
            }

            try
            {
                await DiscordWebhookClient.SendEmbedAsync(
                    prefs.DiscordWebhookUrl,
                    "🔴 Valheim Server Offline",
                    $"**{GetServerName()}** has stopped.",
                    DiscordColorOffline,
                    ("World", GetWorldName(), true),
                    ("Address", GetConnectionAddress(), true),
                    ("Uptime", FormatDuration(UptimeTimer.Elapsed), true));

                Logger.Information("Discord server offline notification sent");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to send Discord server offline notification");
            }
        }

        private async Task SendJoinCodeReadyAsync()
        {
            var prefs = UserPrefsProvider.LoadPreferences();

            if (!prefs.DiscordStatusNotifications ||
                !prefs.DiscordNotifyJoinCode ||
                string.IsNullOrWhiteSpace(prefs.DiscordWebhookUrl))
            {
                return;
            }

            try
            {
                await DiscordWebhookClient.SendEmbedAsync(
                    prefs.DiscordWebhookUrl,
                    "🔑 Join Code Ready",
                    $"**{GetServerName()}** has a new join code.",
                    DiscordColorJoinCode,
                    ("Join Code", CurrentJoinCode, true),
                    ("Address", GetConnectionAddress(), true),
                    ("Uptime", FormatDuration(UptimeTimer.Elapsed), true));

                Logger.Information("Discord join code notification sent");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to send Discord join code notification");
            }
        }

        private async Task SendPlayerDiedAsync(string playerName)
        {
            var prefs = UserPrefsProvider.LoadPreferences();

            if (!prefs.DiscordStatusNotifications ||
                !prefs.DiscordNotifyPlayerDied ||
                string.IsNullOrWhiteSpace(prefs.DiscordWebhookUrl))
            {
                return;
            }

            try
            {
                await DiscordWebhookClient.SendAsync(
                    prefs.DiscordWebhookUrl,
                    $"💀 RIP **{playerName}** 🪦");

                Logger.Information("Discord player death notification sent: {player}", playerName);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to send Discord player death notification");
            }
        }

        private async Task SendPlayerJoinedAsync(PlayerInfo player)
        {
            var prefs = UserPrefsProvider.LoadPreferences();
            int playerCount;

            if (!prefs.DiscordStatusNotifications ||
                !prefs.DiscordNotifyPlayerJoined ||
                string.IsNullOrWhiteSpace(prefs.DiscordWebhookUrl))
            {
                return;
            }

            lock (SyncRoot) { playerCount = OnlinePlayerKeys.Count; }

            try
            {
                await DiscordWebhookClient.SendEmbedAsync(
                    prefs.DiscordWebhookUrl,
                    "👤 Player Joined",
                    $"**{GetPlayerDisplayName(player)}** joined the server.",
                    DiscordColorPlayerJoin,
                    ("Players Online", playerCount.ToString(), true),
                    ("Server Uptime", FormatDuration(UptimeTimer.Elapsed), true),
                    ("World", GetWorldName(), true));

                Logger.Information("Discord player joined notification sent: {player}", GetPlayerDisplayName(player));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to send Discord player joined notification");
            }
        }

        private async Task SendPlayerLeftAsync(PlayerInfo player, DateTimeOffset joinedAt)
        {
            var prefs = UserPrefsProvider.LoadPreferences();
            int playerCount;

            if (!prefs.DiscordStatusNotifications ||
                !prefs.DiscordNotifyPlayerLeft ||
                string.IsNullOrWhiteSpace(prefs.DiscordWebhookUrl))
            {
                return;
            }

            lock (SyncRoot) { playerCount = OnlinePlayerKeys.Count; }

            try
            {
                var sessionDuration = joinedAt == default
                    ? "Unknown"
                    : FormatDuration(DateTimeOffset.UtcNow - joinedAt);

                await DiscordWebhookClient.SendEmbedAsync(
                    prefs.DiscordWebhookUrl,
                    "👋 Player Left",
                    $"**{GetPlayerDisplayName(player)}** left the server.",
                    DiscordColorPlayerLeave,
                    ("Players Online", playerCount.ToString(), true),
                    ("Session Time", sessionDuration, true),
                    ("Server Uptime", FormatDuration(UptimeTimer.Elapsed), true));

                Logger.Information("Discord player left notification sent: {player}", GetPlayerDisplayName(player));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to send Discord player left notification");
            }
        }

        #endregion

        #region Helper methods

        private string GetServerName()
        {
            var name = Server.Options?.Name;
            return string.IsNullOrWhiteSpace(name) ? "Valheim Server" : name;
        }

        private string GetWorldName()
        {
            var world = Server.Options?.WorldName;
            return string.IsNullOrWhiteSpace(world) ? "Unknown" : world;
        }

        private string GetConnectionAddress()
        {
            var ip = IpAddressProvider.ExternalIpAddress;

            if (string.IsNullOrWhiteSpace(ip))
            {
                ip = IpAddressProvider.InternalIpAddress;
            }

            if (string.IsNullOrWhiteSpace(ip))
            {
                ip = "Unavailable";
            }

            var port = Server.Options?.Port ?? 0;
            return $"{ip}:{port}";
        }

        private static string GetPlayerKey(PlayerInfo player)
        {
            if (!string.IsNullOrWhiteSpace(player.Key)) return player.Key;

            return $"{player.Platform}:{player.PlayerId}:{player.LastStatusCharacter}";
        }

        private static string GetPlayerDisplayName(PlayerInfo player)
        {
            if (!string.IsNullOrWhiteSpace(player.LastStatusCharacter)) return player.LastStatusCharacter;
            if (!string.IsNullOrWhiteSpace(player.PlayerName)) return player.PlayerName;
            if (!string.IsNullOrWhiteSpace(player.PlayerId)) return player.PlayerId;

            return "Unknown Player";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

            if (duration.TotalDays >= 1)
            {
                return $"{(int)duration.TotalDays}d {duration.Hours:00}h {duration.Minutes:00}m";
            }

            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes:00}m {duration.Seconds:00}s";
            }

            return $"{duration.Minutes}m {duration.Seconds:00}s";
        }

        #endregion

        public void Dispose()
        {
            Server.StatusChanged -= OnServerStatusChanged;
            Server.InviteCodeReady -= OnInviteCodeReady;
            Server.PlayerDied -= OnPlayerDied;
            PlayerDataProvider.PlayerStatusChanged -= OnPlayerStatusChanged;
        }
    }
}
