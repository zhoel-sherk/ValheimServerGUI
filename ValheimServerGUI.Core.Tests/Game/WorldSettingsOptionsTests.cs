using System.Linq;
using ValheimServerGUI.Game;
using Xunit;

namespace ValheimServerGUI.Core.Tests.Game
{
    public class WorldSettingsOptionsTests
    {
        [Fact]
        public void ApplyPreset_Easy()
        {
            var settings = WorldSettingsOptions.ApplyPreset(WorldGenPresets.Easy);

            Assert.Equal(WorldGenPresets.Easy, settings.Preset);
            Assert.Equal(WorldGenModifiers.Values.CombatEasy, settings.Modifiers[WorldGenModifiers.Combat]);
            Assert.Equal(WorldGenModifiers.Values.RaidsLess, settings.Modifiers[WorldGenModifiers.Raids]);
            Assert.Empty(settings.Keys);
        }

        [Fact]
        public void ApplyPreset_Normal_HasNoOverrides()
        {
            var settings = WorldSettingsOptions.ApplyPreset(WorldGenPresets.Normal);

            Assert.Equal(WorldGenPresets.Normal, settings.Preset);
            Assert.Empty(settings.Modifiers);
            Assert.Empty(settings.Keys);
        }

        [Fact]
        public void ApplyPreset_Hardcore()
        {
            var settings = WorldSettingsOptions.ApplyPreset(WorldGenPresets.Hardcore);

            Assert.Equal(WorldGenModifiers.Values.CombatVeryHard, settings.Modifiers[WorldGenModifiers.Combat]);
            Assert.Equal(WorldGenModifiers.Values.DeathPenaltyHardcore, settings.Modifiers[WorldGenModifiers.DeathPenalty]);
            Assert.Equal(WorldGenModifiers.Values.RaidsMore, settings.Modifiers[WorldGenModifiers.Raids]);
            Assert.Equal(WorldGenModifiers.Values.PortalsHard, settings.Modifiers[WorldGenModifiers.Portals]);
            Assert.Contains(WorldGenKeys.NoMap, settings.Keys);
        }

        [Fact]
        public void ApplyPreset_Casual()
        {
            var settings = WorldSettingsOptions.ApplyPreset(WorldGenPresets.Casual);

            Assert.Equal(WorldGenModifiers.Values.CombatVeryEasy, settings.Modifiers[WorldGenModifiers.Combat]);
            Assert.Equal(WorldGenModifiers.Values.DeathPenaltyCasual, settings.Modifiers[WorldGenModifiers.DeathPenalty]);
            Assert.Equal(WorldGenModifiers.Values.ResourcesMore, settings.Modifiers[WorldGenModifiers.Resources]);
            Assert.Equal(WorldGenModifiers.Values.RaidsNone, settings.Modifiers[WorldGenModifiers.Raids]);
            Assert.Equal(WorldGenModifiers.Values.PortalsCasual, settings.Modifiers[WorldGenModifiers.Portals]);
            Assert.Contains(WorldGenKeys.PlayerEvents, settings.Keys);
            Assert.Contains(WorldGenKeys.PassiveMobs, settings.Keys);
        }

        [Fact]
        public void ApplyPreset_Hammer()
        {
            var settings = WorldSettingsOptions.ApplyPreset(WorldGenPresets.Hammer);

            Assert.Equal(WorldGenModifiers.Values.RaidsNone, settings.Modifiers[WorldGenModifiers.Raids]);
            Assert.Contains(WorldGenKeys.NoBuildCost, settings.Keys);
            Assert.Contains(WorldGenKeys.PassiveMobs, settings.Keys);
        }

        [Fact]
        public void ApplyPreset_Immersive()
        {
            var settings = WorldSettingsOptions.ApplyPreset(WorldGenPresets.Immersive);

            Assert.Equal(WorldGenModifiers.Values.PortalsVeryHard, settings.Modifiers[WorldGenModifiers.Portals]);
            Assert.Contains(WorldGenKeys.NoMap, settings.Keys);
            Assert.Contains(WorldGenKeys.Fire, settings.Keys);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("unknown-preset")]
        public void ApplyPreset_CustomOrUnknown_HasNoOverrides(string preset)
        {
            var settings = WorldSettingsOptions.ApplyPreset(preset);

            Assert.Empty(settings.Modifiers);
            Assert.Empty(settings.Keys);
        }

        [Fact]
        public void PresetOptions_CoverEveryGamePreset()
        {
            var values = WorldSettingsOptions.PresetOptions.Select(o => o.Value).Where(v => v != WorldSettingsOptions.NoPreset);

            foreach (var preset in WorldGenPresets.All)
            {
                Assert.Contains(preset, values);
            }
        }

        [Fact]
        public void ModifierOptions_CoverEveryGameModifierAndValue()
        {
            var modifierKeys = WorldSettingsOptions.ModifierOptions.Select(m => m.Key).ToList();
            Assert.Equal(WorldGenModifiers.All.OrderBy(k => k), modifierKeys.OrderBy(k => k));

            foreach (var modifier in WorldSettingsOptions.ModifierOptions)
            {
                var allowed = WorldGenModifiers.AllowedValues[modifier.Key];
                var offered = modifier.Options.Select(o => o.Value).Where(v => v != WorldSettingsOptions.NoModifier);

                Assert.Equal(allowed.OrderBy(v => v), offered.OrderBy(v => v));
            }
        }

        [Fact]
        public void KeyOptions_CoverEveryGameKey()
        {
            var keys = WorldSettingsOptions.KeyOptions.Select(k => k.Key).ToList();
            Assert.Equal(WorldGenKeys.All.OrderBy(k => k), keys.OrderBy(k => k));
        }

        [Fact]
        public void GetPresetDisplayName_RoundTrips()
        {
            foreach (var option in WorldSettingsOptions.PresetOptions)
            {
                Assert.Equal(option.DisplayName, WorldSettingsOptions.GetPresetDisplayName(option.Value));
            }
        }

        [Fact]
        public void GetModifierDisplayName_RoundTrips()
        {
            foreach (var modifier in WorldSettingsOptions.ModifierOptions)
            {
                foreach (var option in modifier.Options)
                {
                    Assert.Equal(option.DisplayName, WorldSettingsOptions.GetModifierDisplayName(modifier.Key, option.Value));
                }
            }
        }

        [Fact]
        public void GetDisplayName_FallsBackToNormalOrCustom()
        {
            Assert.Equal(WorldSettingsOptions.NoPresetDisplayName, WorldSettingsOptions.GetPresetDisplayName("nope"));
            Assert.Equal(WorldSettingsOptions.NoModifierDisplayName, WorldSettingsOptions.GetModifierDisplayName(WorldGenModifiers.Combat, "nope"));
        }
    }
}
