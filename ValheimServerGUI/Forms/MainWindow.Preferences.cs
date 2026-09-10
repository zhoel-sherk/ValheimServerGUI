using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ValheimServerGUI.Controls;
using ValheimServerGUI.Game;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Properties;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;
using ValheimServerGUI.Tools.Models;

namespace ValheimServerGUI.Forms
{
    public partial class MainWindow
    {
        #region Save & Load

        private ServerPreferences GetPrefsFromFormState()
        {
            var profileName = CurrentProfile;

            // Update existing prefs if they exist with this server name
            // Otherwise, create new prefs with this profile name
            // NOTE: profileName may be null if no profile has been loaded yet (e.g. during form init)
            var prefs = profileName == null
                ? new ServerPreferences()
                : ServerPrefsProvider.LoadPreferences(profileName) ?? new ServerPreferences { ProfileName = profileName };

            prefs.Name = ServerNameField.Value;
            prefs.Port = ServerPortField.Value;
            prefs.Password = ServerPasswordField.Value;
            prefs.WorldName = WorldSelectRadioNew.Value
                ? WorldSelectNewNameField.Value
                : WorldSelectExistingNameField.Value;
            prefs.Public = CommunityServerField.Value;
            prefs.Crossplay = ServerCrossplayField.Value;
            prefs.SaveInterval = ServerSaveIntervalField.Value;
            prefs.BackupCount = ServerBackupsField.Value;
            prefs.BackupIntervalShort = ServerShortBackupIntervalField.Value;
            prefs.BackupIntervalLong = ServerLongBackupIntervalField.Value;
            prefs.AutoStart = ServerAutoStartField.Value;
            prefs.AdditionalArgs = ServerAdditionalArgsField.Value;
            prefs.ServerExePath = ServerExePathField.Value;
            prefs.SaveDataFolderPath = ServerSaveDataFolderPathField.Value;
            prefs.WriteServerLogsToFile = ServerLogFileField.Value;

            return prefs;
        }

        private ValheimServerOptions GetServerOptionsFromFormState()
        {
            var userPrefs = UserPrefsProvider.LoadPreferences();
            var serverPrefs = GetPrefsFromFormState();

            var options = new ValheimServerOptions
            {
                Name = serverPrefs.Name,
                Password = serverPrefs.Password,
                PasswordValidation = userPrefs.EnablePasswordValidation,
                WorldName = serverPrefs.WorldName, // Server automatically creates a new world if a world doesn't yet exist w/ that name
                Public = serverPrefs.Public,
                Port = serverPrefs.Port,
                Crossplay = serverPrefs.Crossplay,
                SaveInterval = serverPrefs.SaveInterval,
                Backups = serverPrefs.BackupCount,
                BackupShort = serverPrefs.BackupIntervalShort,
                BackupLong = serverPrefs.BackupIntervalLong,
                AdditionalArgs = serverPrefs.AdditionalArgs,
                ServerExePath = !string.IsNullOrWhiteSpace(serverPrefs.ServerExePath)
                    ? serverPrefs.ServerExePath
                    : userPrefs.ServerExePath,
                SaveDataFolderPath = !string.IsNullOrWhiteSpace(serverPrefs.SaveDataFolderPath)
                    ? serverPrefs.SaveDataFolderPath
                    : userPrefs.SaveDataFolderPath,
                LogToFile = serverPrefs.WriteServerLogsToFile,
                LogMessageHandler = this.BuildActionHandler<string>(OnServerLogReceived),
            };

            // If a world is selected, load preferences for that world
            var worldName = serverPrefs.WorldName;
            if (!string.IsNullOrWhiteSpace(worldName))
            {
                var worldPrefs = WorldPrefsProvider.LoadPreferences(worldName);
                if (worldPrefs != null)
                {
                    if (!string.IsNullOrEmpty(worldPrefs.Preset))
                    {
                        options.WorldPreset = worldPrefs.Preset;
                    }
                    else
                    {
                        options.WorldModifiers = worldPrefs.Modifiers;
                    }

                    options.WorldKeys = worldPrefs.Keys;
                }
            }

            return options;
        }

        private ServerPreferences SetFormStateFromPrefs(string profileName)
        {
            var prefs = ServerPrefsProvider.LoadPreferences(profileName);
            if (prefs == null)
            {
                Logger.Warning("Unable to set form state: no server profile exists with name '{name}'", profileName);
                return prefs;
            }

            SetFormStateFromPrefs(prefs);
            return prefs;
        }

        private void SetFormStateFromPrefs(ServerPreferences prefs)
        {
            if (prefs == null)
            {
                Logger.Warning($"Unable to set form state: {nameof(prefs)} cannot be null");
                return;
            }

            CurrentProfile = prefs.ProfileName;

            ServerNameField.Value = prefs.Name;
            ServerPortField.Value = prefs.Port;
            ServerPasswordField.Value = prefs.Password;
            ShowPasswordField.Value = false;
            CommunityServerField.Value = prefs.Public;
            ServerCrossplayField.Value = prefs.Crossplay;
            ServerSaveIntervalField.Value = prefs.SaveInterval;
            ServerBackupsField.Value = prefs.BackupCount;
            ServerShortBackupIntervalField.Value = prefs.BackupIntervalShort;
            ServerLongBackupIntervalField.Value = prefs.BackupIntervalLong;
            ServerAutoStartField.Value = prefs.AutoStart;
            ServerAdditionalArgsField.Value = prefs.AdditionalArgs;
            ServerExePathField.Value = prefs.ServerExePath;
            ServerSaveDataFolderPathField.Value = prefs.SaveDataFolderPath;
            ServerLogFileField.Value = prefs.WriteServerLogsToFile;

            RefreshWorldSelect();
            var worldName = prefs.WorldName;

            if (WorldSelectExistingNameField.DataSource != null &&
                WorldSelectExistingNameField.DataSource.Contains(worldName))
            {
                WorldSelectExistingNameField.Value = worldName;
                WorldSelectRadioExisting.Value = true;
            }
            else
            {
                WorldSelectNewNameField.Value = worldName;
                WorldSelectRadioNew.Value = true;
            }
        }

        #endregion
    }
}
