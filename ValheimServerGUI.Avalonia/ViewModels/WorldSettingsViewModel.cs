using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
using ValheimServerGUI.Infrastructure;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>A world modifier row (label + dropdown).</summary>
    public partial class WorldModifierRowViewModel : ObservableObject
    {
        public WorldModifierRowViewModel(WorldSettingModifier modifier)
        {
            Modifier = modifier;
            _selectedOption = modifier.Options.First(o => o.Value == WorldSettingsOptions.NoModifier);
        }

        public WorldSettingModifier Modifier { get; }

        public string DisplayName => Modifier.DisplayName;

        public IReadOnlyList<WorldSettingOption> Options => Modifier.Options;

        [ObservableProperty]
        private WorldSettingOption _selectedOption;

        public string Value => SelectedOption?.Value ?? WorldSettingsOptions.NoModifier;

        public void SetValue(string value)
        {
            SelectedOption = Options.FirstOrDefault(o => o.Value == (value ?? WorldSettingsOptions.NoModifier))
                ?? Options.First(o => o.Value == WorldSettingsOptions.NoModifier);
        }
    }

    /// <summary>An additional world key row (checkbox).</summary>
    public partial class WorldKeyRowViewModel : ObservableObject
    {
        public WorldKeyRowViewModel(WorldSettingKey key)
        {
            Key = key;
        }

        public WorldSettingKey Key { get; }

        public string DisplayName => Key.DisplayName;

        [ObservableProperty]
        private bool _isChecked;
    }

    /// <summary>
    /// World difficulty settings dialog: preset plus modifiers/keys, mirroring the in-game
    /// World Modifiers menu (and the old WinForms WorldPreferencesForm).
    /// </summary>
    public partial class WorldSettingsViewModel : ObservableObject
    {
        private readonly IWorldPreferencesProvider WorldPrefsProvider;
        private readonly IPlatformIntegration PlatformIntegration;
        private readonly IApplicationLogger Logger;

        private bool IsApplyingPreset;

        public ObservableCollection<WorldModifierRowViewModel> Modifiers { get; } = new();

        public ObservableCollection<WorldKeyRowViewModel> Keys { get; } = new();

        public IReadOnlyList<WorldSettingOption> Presets => WorldSettingsOptions.PresetOptions;

        [ObservableProperty]
        private string? _worldName;

        [ObservableProperty]
        private WorldSettingOption _selectedPreset;

        public WorldSettingsViewModel(
            IWorldPreferencesProvider worldPrefsProvider,
            IPlatformIntegration platformIntegration,
            IApplicationLogger logger)
        {
            WorldPrefsProvider = worldPrefsProvider;
            PlatformIntegration = platformIntegration;
            Logger = logger;

            foreach (var modifier in WorldSettingsOptions.ModifierOptions)
            {
                var row = new WorldModifierRowViewModel(modifier);
                row.PropertyChanged += OnRowChanged;
                Modifiers.Add(row);
            }

            foreach (var key in WorldSettingsOptions.KeyOptions)
            {
                var row = new WorldKeyRowViewModel(key);
                row.PropertyChanged += OnRowChanged;
                Keys.Add(row);
            }

            _selectedPreset = Presets[0];
        }

        public string Title => string.IsNullOrWhiteSpace(WorldName)
            ? "World Settings"
            : $"World Settings - {WorldName}";

        /// <summary>Call before showing the dialog.</summary>
        public void Load(string worldName)
        {
            WorldName = worldName;
            OnPropertyChanged(nameof(Title));

            var prefs = WorldPrefsProvider.LoadPreferences(worldName);

            if (prefs == null || string.IsNullOrWhiteSpace(prefs.Preset))
            {
                SetPreset(WorldSettingsOptions.NoPreset);

                if (prefs != null)
                {
                    foreach (var row in Modifiers)
                    {
                        row.SetValue(prefs.Modifiers != null && prefs.Modifiers.TryGetValue(row.Modifier.Key, out var value)
                            ? value
                            : WorldSettingsOptions.NoModifier);
                    }

                    foreach (var row in Keys)
                    {
                        row.IsChecked = prefs.Keys != null && prefs.Keys.Contains(row.Key.Key);
                    }
                }
            }
            else
            {
                SetPreset(prefs.Preset);
            }
        }

        [RelayCommand]
        private void RestoreDefaults()
        {
            SetPreset(WorldSettingsOptions.NoPreset);
        }

        [RelayCommand]
        private void OpenModifiersHelp()
        {
            PlatformIntegration.OpenWebAddress(AppSettings.UrlValheimWikiWorldModifiers);
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(WorldName))
            {
                Logger.Error("Unable to save world settings: no world name has been set");
                return;
            }

            var prefs = WorldPrefsProvider.LoadPreferences(WorldName)
                ?? new WorldPreferences { WorldName = WorldName };

            prefs.Preset = null;
            prefs.Modifiers.Clear();
            prefs.Keys.Clear();

            if (SelectedPreset != null && SelectedPreset.Value != WorldSettingsOptions.NoPreset)
            {
                prefs.Preset = SelectedPreset.Value;
            }
            else
            {
                foreach (var row in Modifiers)
                {
                    if (row.Value != WorldSettingsOptions.NoModifier) prefs.Modifiers[row.Modifier.Key] = row.Value;
                }

                foreach (var row in Keys)
                {
                    if (row.IsChecked) prefs.Keys.Add(row.Key.Key);
                }
            }

            WorldPrefsProvider.SavePreferences(prefs);
            Logger.Information("Saved world settings for {world}", WorldName);
        }

        private void SetPreset(string presetValue)
        {
            IsApplyingPreset = true;
            try
            {
                var settings = WorldSettingsOptions.ApplyPreset(presetValue);

                foreach (var row in Modifiers)
                {
                    row.SetValue(settings.Modifiers.TryGetValue(row.Modifier.Key, out var value)
                        ? value
                        : WorldSettingsOptions.NoModifier);
                }

                foreach (var row in Keys)
                {
                    row.IsChecked = settings.Keys.Contains(row.Key.Key);
                }

                SelectedPreset = Presets.FirstOrDefault(p => p.Value == settings.Preset) ?? Presets[0];
            }
            finally
            {
                IsApplyingPreset = false;
            }
        }

        private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsApplyingPreset) return;

            // Any manual change invalidates a selected preset, same as in-game.
            if (SelectedPreset == null || SelectedPreset.Value == WorldSettingsOptions.NoPreset) return;

            IsApplyingPreset = true;
            try
            {
                SelectedPreset = Presets[0];
            }
            finally
            {
                IsApplyingPreset = false;
            }
        }

        partial void OnSelectedPresetChanged(WorldSettingOption value)
        {
            if (IsApplyingPreset || value == null) return;
            SetPreset(value.Value);
        }
    }
}
