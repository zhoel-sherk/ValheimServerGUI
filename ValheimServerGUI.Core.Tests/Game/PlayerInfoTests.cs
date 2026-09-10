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

            Assert.False(PlayerPlatforms.TryGetValidPlatform("PlayStation", out _));
            Assert.False(PlayerPlatforms.TryGetValidPlatform(null, out _));
        }
    }
}