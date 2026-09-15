using System.Collections.Generic;
using System.Linq;

namespace ValheimServerGUI.Game
{
    /// <summary>A named option shown in a world settings dropdown.</summary>
    public sealed class WorldSettingOption
    {
        public WorldSettingOption(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }

        public string DisplayName { get; }

        public string Value { get; }
    }

    /// <summary>A world modifier (e.g. Combat) with its selectable values.</summary>
    public sealed class WorldSettingModifier
    {
        public WorldSettingModifier(string key, string displayName, IReadOnlyList<WorldSettingOption> options)
        {
            Key = key;
            DisplayName = displayName;
            Options = options;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public IReadOnlyList<WorldSettingOption> Options { get; }
    }

    /// <summary>An additional world key (e.g. "No map").</summary>
    public sealed class WorldSettingKey
    {
        public WorldSettingKey(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public string Key { get; }

        public string DisplayName { get; }
    }

    /// <summary>The preset/modifier/key selection for a world.</summary>
    public sealed class WorldSettings
    {
        public string Preset { get; set; }

        public Dictionary<string, string> Modifiers { get; set; } = new();

        public HashSet<string> Keys { get; set; } = new();
    }

    /// <summary>
    /// Display names and preset rules for Valheim world difficulty settings, shared by every
    /// client. Presets override modifiers/keys, exactly like the in-game World Modifiers menu.
    /// </summary>
    public static class WorldSettingsOptions
    {
        public const string NoPreset = "";
        public const string NoModifier = "";

        public const string NoPresetDisplayName = "Custom (No Preset)";
        public const string NoModifierDisplayName = "Normal";

        public static IReadOnlyList<WorldSettingOption> PresetOptions { get; } = new List<WorldSettingOption>
        {
            new(NoPresetDisplayName, NoPreset),
            new("Easy", WorldGenPresets.Easy),
            new("Normal", WorldGenPresets.Normal),
            new("Hard", WorldGenPresets.Hard),
            new("Hardcore", WorldGenPresets.Hardcore),
            new("Casual", WorldGenPresets.Casual),
            new("Hammer Mode", WorldGenPresets.Hammer),
            new("Immersive", WorldGenPresets.Immersive),
        };

        public static IReadOnlyList<WorldSettingModifier> ModifierOptions { get; } = new List<WorldSettingModifier>
        {
            new(WorldGenModifiers.Combat, "Combat", new List<WorldSettingOption>
            {
                new("Very Easy", WorldGenModifiers.Values.CombatVeryEasy),
                new("Easy", WorldGenModifiers.Values.CombatEasy),
                new(NoModifierDisplayName, NoModifier),
                new("Hard", WorldGenModifiers.Values.CombatHard),
                new("Very Hard", WorldGenModifiers.Values.CombatVeryHard),
            }),
            new(WorldGenModifiers.DeathPenalty, "Death penalty", new List<WorldSettingOption>
            {
                new("Casual", WorldGenModifiers.Values.DeathPenaltyCasual),
                new("Very Easy", WorldGenModifiers.Values.DeathPenaltyVeryEasy),
                new("Easy", WorldGenModifiers.Values.DeathPenaltyEasy),
                new(NoModifierDisplayName, NoModifier),
                new("Hard", WorldGenModifiers.Values.DeathPenaltyHard),
                new("Hardcore", WorldGenModifiers.Values.DeathPenaltyHardcore),
            }),
            new(WorldGenModifiers.Resources, "Resource Rate", new List<WorldSettingOption>
            {
                new("Much Less (0.5x)", WorldGenModifiers.Values.ResourcesMuchLess),
                new("Less (0.75x)", WorldGenModifiers.Values.ResourcesLess),
                new(NoModifierDisplayName, NoModifier),
                new("More (1.5x)", WorldGenModifiers.Values.ResourcesMore),
                new("Much More (2x)", WorldGenModifiers.Values.ResourcesMuchMore),
                new("Most (3x)", WorldGenModifiers.Values.ResourcesMost),
            }),
            new(WorldGenModifiers.Raids, "Raid Rate", new List<WorldSettingOption>
            {
                new("None", WorldGenModifiers.Values.RaidsNone),
                new("Much Less", WorldGenModifiers.Values.RaidsMuchLess),
                new("Less", WorldGenModifiers.Values.RaidsLess),
                new(NoModifierDisplayName, NoModifier),
                new("More", WorldGenModifiers.Values.RaidsMore),
                new("Much More", WorldGenModifiers.Values.RaidsMuchMore),
            }),
            new(WorldGenModifiers.Portals, "Portals", new List<WorldSettingOption>
            {
                new("Casual (Portal items)", WorldGenModifiers.Values.PortalsCasual),
                new(NoModifierDisplayName, NoModifier),
                new("Hard (No boss portals)", WorldGenModifiers.Values.PortalsHard),
                new("Very Hard (No portals)", WorldGenModifiers.Values.PortalsVeryHard),
            }),
        };

        public static IReadOnlyList<WorldSettingKey> KeyOptions { get; } = new List<WorldSettingKey>
        {
            new(WorldGenKeys.NoBuildCost, "No build cost"),
            new(WorldGenKeys.PlayerEvents, "Player based raids"),
            new(WorldGenKeys.PassiveMobs, "Passive enemies"),
            new(WorldGenKeys.NoMap, "No map"),
            new(WorldGenKeys.Fire, "Fire hazards"),
        };

        /// <summary>
        /// The modifiers and keys a preset implies. Returns an empty set for "custom" (no preset)
        /// or an unknown preset. Normal intentionally yields no modifiers.
        /// </summary>
        public static WorldSettings ApplyPreset(string preset)
        {
            var settings = new WorldSettings { Preset = string.IsNullOrWhiteSpace(preset) ? NoPreset : preset };

            void Modifier(string key, string value) => settings.Modifiers[key] = value;
            void Key(string key) => settings.Keys.Add(key);

            switch (settings.Preset)
            {
                case WorldGenPresets.Easy:
                    Modifier(WorldGenModifiers.Combat, WorldGenModifiers.Values.CombatEasy);
                    Modifier(WorldGenModifiers.Raids, WorldGenModifiers.Values.RaidsLess);
                    break;

                case WorldGenPresets.Hard:
                    Modifier(WorldGenModifiers.Combat, WorldGenModifiers.Values.CombatHard);
                    Modifier(WorldGenModifiers.Raids, WorldGenModifiers.Values.RaidsMore);
                    break;

                case WorldGenPresets.Hardcore:
                    Modifier(WorldGenModifiers.Combat, WorldGenModifiers.Values.CombatVeryHard);
                    Modifier(WorldGenModifiers.DeathPenalty, WorldGenModifiers.Values.DeathPenaltyHardcore);
                    Modifier(WorldGenModifiers.Raids, WorldGenModifiers.Values.RaidsMore);
                    Modifier(WorldGenModifiers.Portals, WorldGenModifiers.Values.PortalsHard);
                    Key(WorldGenKeys.NoMap);
                    break;

                case WorldGenPresets.Casual:
                    Modifier(WorldGenModifiers.Combat, WorldGenModifiers.Values.CombatVeryEasy);
                    Modifier(WorldGenModifiers.DeathPenalty, WorldGenModifiers.Values.DeathPenaltyCasual);
                    Modifier(WorldGenModifiers.Resources, WorldGenModifiers.Values.ResourcesMore);
                    Modifier(WorldGenModifiers.Raids, WorldGenModifiers.Values.RaidsNone);
                    Modifier(WorldGenModifiers.Portals, WorldGenModifiers.Values.PortalsCasual);
                    Key(WorldGenKeys.PlayerEvents);
                    Key(WorldGenKeys.PassiveMobs);
                    break;

                case WorldGenPresets.Hammer:
                    Modifier(WorldGenModifiers.Raids, WorldGenModifiers.Values.RaidsNone);
                    Key(WorldGenKeys.NoBuildCost);
                    Key(WorldGenKeys.PassiveMobs);
                    break;

                case WorldGenPresets.Immersive:
                    Modifier(WorldGenModifiers.Portals, WorldGenModifiers.Values.PortalsVeryHard);
                    Key(WorldGenKeys.NoMap);
                    Key(WorldGenKeys.Fire);
                    break;

                case WorldGenPresets.Normal:
                case NoPreset:
                default:
                    break;
            }

            return settings;
        }

        public static string GetPresetDisplayName(string presetValue)
        {
            return PresetOptions.FirstOrDefault(o => o.Value == (presetValue ?? NoPreset))?.DisplayName
                ?? NoPresetDisplayName;
        }

        public static string GetModifierDisplayName(string modifierKey, string modifierValue)
        {
            var modifier = ModifierOptions.FirstOrDefault(m => m.Key == modifierKey);
            return modifier?.Options.FirstOrDefault(o => o.Value == (modifierValue ?? NoModifier))?.DisplayName
                ?? NoModifierDisplayName;
        }

        public static string GetKeyDisplayName(string key)
        {
            return KeyOptions.FirstOrDefault(k => k.Key == key)?.DisplayName ?? key;
        }
    }
}
