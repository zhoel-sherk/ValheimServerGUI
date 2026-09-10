using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Application preferences dialog (Phase 2 step 8).
    /// </summary>
    public partial class PreferencesViewModel : ObservableObject
    {
        private readonly IUserPreferencesProvider UserPrefsProvider;
        private readonly IApplicationLogger Logger;

        [ObservableProperty]
        private bool _saveProfileOnStart;

        [ObservableProperty]
        private bool _checkForUpdates;

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private bool _startMinimized;

        [ObservableProperty]
        private bool _writeApplicationLogsToFile;

        [ObservableProperty]
        private bool _enablePasswordValidation;

        public PreferencesViewModel(IUserPreferencesProvider userPrefsProvider, IApplicationLogger logger)
        {
            UserPrefsProvider = userPrefsProvider;
            Logger = logger;

            var prefs = UserPrefsProvider.LoadPreferences();
            SaveProfileOnStart = prefs.SaveProfileOnStart;
            CheckForUpdates = prefs.CheckForUpdates;
            StartWithWindows = prefs.StartWithWindows;
            StartMinimized = prefs.StartMinimized;
            WriteApplicationLogsToFile = prefs.WriteApplicationLogsToFile;
            EnablePasswordValidation = prefs.EnablePasswordValidation;
        }

        [RelayCommand]
        private void ApplyDefaults()
        {
            var prefs = UserPreferences.GetDefault();

            SaveProfileOnStart = prefs.SaveProfileOnStart;
            CheckForUpdates = prefs.CheckForUpdates;
            StartWithWindows = prefs.StartWithWindows;
            StartMinimized = prefs.StartMinimized;
            WriteApplicationLogsToFile = prefs.WriteApplicationLogsToFile;
            EnablePasswordValidation = prefs.EnablePasswordValidation;
        }

        [RelayCommand]
        private void Save()
        {
            var prefs = UserPrefsProvider.LoadPreferences();

            prefs.SaveProfileOnStart = SaveProfileOnStart;
            prefs.CheckForUpdates = CheckForUpdates;
            prefs.StartWithWindows = StartWithWindows;
            prefs.StartMinimized = StartMinimized;
            prefs.WriteApplicationLogsToFile = WriteApplicationLogsToFile;
            prefs.EnablePasswordValidation = EnablePasswordValidation;

            UserPrefsProvider.SavePreferences(prefs);
        }
    }
}