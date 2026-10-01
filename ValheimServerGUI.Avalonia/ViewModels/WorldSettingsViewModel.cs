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

        /// <summary>The settings as they were loaded, used to detect whether the user changed anything.</summary>
        private WorldSettings _loadedSettings = new();

        /// <summary>Whether the server was running when the dialog opened.</summary>
        private bool _isServerRunning;

        public ObservableCollection<WorldModifierRowViewModel> Modifiers { get; } = new();

        public ObservableCollection<WorldKeyRowViewModel> Keys { get; } = new();

        public IReadOnlyList<WorldSettingOption> Presets => WorldSettingsOptions.PresetOptions;

        /// <summary>
        /// True when the server was running when this dialog opened. Difficulty settings are only
        /// passed to the server on launch, so applying them requires a restart.
        /// </summary>
        public bool IsServerRunning
        {
            get => _isServerRunning;
            set
            {
                if (_isServerRunning == value) return;

                _isServerRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RestartNotice));
            }
        }

        /// <summary>Shown when the server is running, explaining that a restart is needed.</summary>
        public string RestartNotice => "The server is currently running. These settings are only passed " +
            "to the server on launch, so applying them will restart it and disconnect everyone connected.";

        /// <summary>
        /// True when the world has already been generated on disk. Difficulty modifiers are baked in
        /// at generation time, so changing them on an existing world only partly takes effect.
        /// </summary>
        public bool WorldAlreadyExists { get; private set; }

        /// <summary>
        /// True once <see cref="ApplyCommand"/> has committed a change that differs from what was
        /// loaded. Lets the caller decide whether a restart is actually needed.
        /// </summary>
        public bool HasChanges { get; private set; }

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
        /// <param name="worldName">The world the settings apply to.</param>
        /// <param name="isServerRunning">
        /// Whether the server is currently running, so the dialog can tell the user that applying
        /// the settings requires a restart.
        /// </param>
        /// <param name="worldAlreadyExists">
        /// Whether the world has already been generated on disk, so the dialog can warn that
        /// difficulty modifiers only take effect for newly generated areas.
        /// </param>
        public void Load(string worldName, bool isServerRunning = false, bool worldAlreadyExists = false)
        {
            WorldName = worldName;
            OnPropertyChanged(nameof(Title));
            IsServerRunning = isServerRunning;
            WorldAlreadyExists = worldAlreadyExists;

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

            // Snapshot what is currently saved so we can tell whether the user actually changed
            // anything, and skip a pointless restart when they just open and close the dialog.
            _loadedSettings = new WorldSettings
            {
                Preset = prefs?.Preset,
                Modifiers = prefs?.Modifiers != null
                    ? new Dictionary<string, string>(prefs.Modifiers)
                    : new Dictionary<string, string>(),
                Keys = prefs?.Keys != null ? new HashSet<string>(prefs.Keys) : new HashSet<string>(),
            };

            HasChanges = false;
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
        private void Apply()
        {
            if (string.IsNullOrWhiteSpace(WorldName))
            {
                Logger.Error("Unable to apply world settings: no world name has been set");
                return;
            }

            var settings = BuildSettings();

            var prefs = new WorldPreferences
            {
                WorldName = WorldName,
                Preset = settings.Preset,
                Modifiers = settings.Modifiers,
                Keys = settings.Keys,
            };

            WorldPrefsProvider.SavePreferences(prefs);
            HasChanges = !WorldSettingsOptions.AreEquivalent(_loadedSettings, settings);
            Logger.Information("Applied world settings for {world}", WorldName);
        }

        /// <summary>
        /// Builds the settings that the current dialog selection represents. A selected preset wins
        /// over individual modifiers/keys, exactly like the in-game menu.
        /// </summary>
        private WorldSettings BuildSettings()
        {
            var settings = new WorldSettings();

            if (SelectedPreset != null && SelectedPreset.Value != WorldSettingsOptions.NoPreset)
            {
                settings.Preset = SelectedPreset.Value;
                return settings;
            }

            foreach (var row in Modifiers)
            {
                if (row.Value != WorldSettingsOptions.NoModifier)
                {
                    settings.Modifiers[row.Modifier.Key] = row.Value;
                }
            }

            foreach (var row in Keys)
            {
                if (row.IsChecked)
                {
                    settings.Keys.Add(row.Key.Key);
                }
            }

            return settings;
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
