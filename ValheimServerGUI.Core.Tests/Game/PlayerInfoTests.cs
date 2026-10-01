using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Models;
using Xunit;

namespace ValheimServerGUI.Core.Tests.Game
{
    public class PlayerInfoTests
    {
        [Fact]
        public void KeyCombinesPlatformAndId()
        {
            var player = new PlayerInfo { Platform = "Steam", PlayerId = "1234" };
            Assert.Equal("Steam:1234", player.Key);
        }

        [Fact]
        public void AddCharacterCreatesAndDeduplicates()
        {
            var player = new PlayerInfo();

            var first = player.AddCharacter("Broheim");
            Assert.Equal("Broheim", first.CharacterName);
            Assert.Single(player.Characters);

            var second = player.AddCharacter("Broheim", matchConfident: false);
            Assert.Same(first, second);
            Assert.False(second.MatchConfident);
            Assert.Single(player.Characters);
        }

        [Fact]
        public void TryGetCharacterReturnsExistingCharacter()
        {
            var player = new PlayerInfo();
            player.AddCharacter("Broheim");

            Assert.True(player.TryGetCharacter("Broheim", out var character));
            Assert.Equal("Broheim", character.CharacterName);
            Assert.False(player.TryGetCharacter("Missing", out _));
        }

        [Fact]
        public void PlayerPlatformsNormalizesInput()
        {
            Assert.True(PlayerPlatforms.TryGetValidPlatform("Steam", out var steam));
            Assert.Equal(PlayerPlatforms.Steam, steam);

            Assert.True(PlayerPlatforms.TryGetValidPlatform("xbox", out var xbox));
            Assert.Equal(PlayerPlatforms.Xbox, xbox);

            Assert.True(PlayerPlatforms.TryGetValidPlatform("playstation", out var playStation));
            Assert.Equal(PlayerPlatforms.PlayStation, playStation);

            // The game's "Switch" token normalizes to Nintendo.
            Assert.True(PlayerPlatforms.TryGetValidPlatform("switch", out var fromSwitch));
            Assert.Equal(PlayerPlatforms.Nintendo, fromSwitch);

            Assert.True(PlayerPlatforms.TryGetValidPlatform("Nintendo", out var nintendo));
            Assert.Equal(PlayerPlatforms.Nintendo, nintendo);

            Assert.True(PlayerPlatforms.TryGetValidPlatform("  STEAM  ", out var padded));
            Assert.Equal(PlayerPlatforms.Steam, padded);

            Assert.False(PlayerPlatforms.TryGetValidPlatform(null, out _));
            Assert.False(PlayerPlatforms.TryGetValidPlatform("", out _));
            Assert.False(PlayerPlatforms.TryGetValidPlatform("   ", out _));
        }

        [Fact]
        public void PlayerPlatformsKnowsTheFourGamePlatforms()
        {
            Assert.Equal(4, PlayerPlatforms.All.Count);
            Assert.Contains(PlayerPlatforms.Steam, PlayerPlatforms.All);
            Assert.Contains(PlayerPlatforms.Xbox, PlayerPlatforms.All);
            Assert.Contains(PlayerPlatforms.PlayStation, PlayerPlatforms.All);
            Assert.Contains(PlayerPlatforms.Nintendo, PlayerPlatforms.All);
        }

        [Theory]
        [InlineData("Epic", "Epic")]
        [InlineData("epic", "Epic")]
        [InlineData("BATTLE.NET", "BATTLE.NET")]
        [InlineData("PlayStation", "PlayStation")]
        public void PlayerPlatformsAcceptsUnknownPlatforms(string input, string expected)
        {
            // Crossplay can report platforms we don't know. Rejecting them would silently drop the
            // player from the list, so anything non-blank is kept.
            Assert.True(PlayerPlatforms.TryGetValidPlatform(input, out var platform));
            Assert.Equal(expected, platform);
        }

        [Fact]
        public void PlayerKeyKeepsUnknownPlatform()
        {
            var player = new PlayerInfo { Platform = "Epic", PlayerId = "abc123" };
            Assert.Equal("Epic:abc123", player.Key);
        }
    }
}