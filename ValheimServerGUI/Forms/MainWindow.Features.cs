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
        #region Feature Capabilities

        private void StartServer(bool isManualStart)
        {
            var onError = (string message) =>
            {
                if (isManualStart)
                {
                    MessageBox.Show(message, "Error starting server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Logger.Error("Error starting server: {message}", message);
                }
            };

            var options = GetServerOptionsFromFormState();

            // Run standard input validation first
            try
            {
                options.Validate();
            }
            catch (Exception exception)
            {
                onError(exception.Message);
                return;
            }

            // Then, run additional validation that requires more context than just the fields themselves
            var port = options.Port;
            if (!IpAddressProvider.IsLocalUdpPortAvailable(port, port + 1))
            {
                onError($"Port {port} or {port + 1} is already in use.{NL}" +
                    $"Valheim requires two adjacent ports to run a dedicated server.{NL}" +
                    "Please shut down any UDP applications using these ports, or choose a different port for your server.");
                return;
            }

            var worldName = options.WorldName;
            var saveFolder = options.GetValidatedSaveDataFolder();
            bool newWorld = WorldSelectRadioNew.Value;

            if (newWorld)
            {
                // Creating a new world, ensure that the name is available
                if (string.IsNullOrWhiteSpace(worldName))
                {
                    onError("You must enter a world name, or choose an existing world.");
                    return;
                }

                if (worldName.Length < 5 || worldName.Length > 20)
                {
                    onError("World name must be 5-20 characters long.");
                    return;
                }

                if (!saveFolder.IsWorldNameAvailable(worldName))
                {
                    onError($"A world named '{worldName}' already exists.");
                    WorldSelectRadioExisting.Value = true;
                    WorldSelectExistingNameField.Value = worldName;
                    return;
                }
            }
            else
            {
                // Using an existing world, ensure that the file exists
                if (saveFolder.IsWorldNameAvailable(worldName))
                {
                    // Don't think this is possible to hit because the name comes from a dropdown
                    onError($"No world exists with name '{worldName}'.");
                    return;
                }
            }

            // Finally, after all validation has finished, try to start the server
            try
            {
                Server.Start(options);
            }
            catch (Exception exception)
            {
                onError(exception.Message);
                return;
            }

            var userPrefs = UserPrefsProvider.LoadPreferences();
            if (userPrefs.SaveProfileOnStart)
            {
                var serverPrefs = GetPrefsFromFormState();
                ServerPrefsProvider.SavePreferences(serverPrefs);
            }
        }

        private void CheckFilePaths()
        {
            try
            {
                var options = GetServerOptionsFromFormState();
                options.GetValidatedServerExe();
            }
            catch (Exception e)
            {
                var result = MessageBox.Show(
                    $"{e.Message}{NL}{NL}" +
                    $"This may occur if you do not have Valheim Dedicated Server installed, or if you have " +
                    $"installed it in a different directory. See Help for more info.{NL}{NL}" +
                    "Would you like to change your directories now?",
                    "File Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    ShowDirectoriesForm();
                }
            }
        }

        private string PromptForProfileName(string startingText = null)
        {
            var dialog = new TextPromptPopout("Server Profile Name", "Enter a server profile name:", startingText);
            dialog.SetValidation(
                "Profile name must be 1-30 characters, and must not match an existing profile name.",
                (input) =>
                {
                    if (string.IsNullOrWhiteSpace(input) || input.Length > 30) return false;

                    var prefs = ServerPrefsProvider.LoadPreferences(input);
                    return prefs == null;
                });

            var result = dialog.ShowDialog();
            if (result != DialogResult.OK) return null;

            return dialog.Value;
        }

        private void LaunchNewWindow()
        {
            var splashForm = FormProvider.GetForm<SplashForm>();
            var mainWindow = splashForm.CreateNewMainWindow(CurrentProfile, false);
            mainWindow.Show();
        }

        private void RefocusWindow()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
        }

        private void CheckForUpdates(bool isManualCheck)
        {
            Task.Run(() => SoftwareUpdateProvider.CheckForUpdatesAsync(isManualCheck));
        }

        private void CloseWindowOnServerStopped()
        {
            if (Server.IsAnyStatus(ServerStatus.Stopped))
            {
                Close();
                return;
            }

            Server.StatusChanged += this.BuildEventHandler<ServerStatus>((status) =>
            {
                if (status == ServerStatus.Stopped)
                {
                    Close();
                }
            });
        }

        #endregion
    }
}
