using ValheimServerGUI.Game;
using Xunit;

namespace ValheimServerGUI.Tests.Game
{
    /// <summary>
    /// Locks the Discord notification preferences round-trip (UserPreferences &lt;-&gt;
    /// UserPreferencesFile), so the webhook and per-event toggles survive a save/load cycle.
    /// </summary>
    public class UserPreferencesTests
    {
        [Fact]
        public void DiscordPreferences_RoundTripThroughFile()
        {
            var prefs = new UserPreferences
            {
                DiscordStatusNotifications = true,
                DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
                DiscordNotifyServerOnline = false,
                DiscordNotifyServerOffline = true,
                DiscordNotifyPlayerJoined = false,
                DiscordNotifyPlayerLeft = true,
                DiscordNotifyPlayerDied = false,
                DiscordNotifyJoinCode = true,
            };

            var reloaded = UserPreferences.FromFile(prefs.ToFile());

            Assert.True(reloaded.DiscordStatusNotifications);
            Assert.Equal("https://discord.com/api/webhooks/123/abc", reloaded.DiscordWebhookUrl);
            Assert.False(reloaded.DiscordNotifyServerOnline);
            Assert.True(reloaded.DiscordNotifyServerOffline);
            Assert.False(reloaded.DiscordNotifyPlayerJoined);
            Assert.True(reloaded.DiscordNotifyPlayerLeft);
            Assert.False(reloaded.DiscordNotifyPlayerDied);
            Assert.True(reloaded.DiscordNotifyJoinCode);
        }

        [Fact]
        public void DiscordPerEventToggles_DefaultToTrue()
        {
            var prefs = new UserPreferences();

            Assert.False(prefs.DiscordStatusNotifications);
            Assert.True(prefs.DiscordNotifyServerOnline);
            Assert.True(prefs.DiscordNotifyServerOffline);
            Assert.True(prefs.DiscordNotifyPlayerJoined);
            Assert.True(prefs.DiscordNotifyPlayerLeft);
            Assert.True(prefs.DiscordNotifyPlayerDied);
            Assert.True(prefs.DiscordNotifyJoinCode);
        }
    }
}
