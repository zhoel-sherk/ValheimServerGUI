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
        #region Form Field Events

        private void ButtonStartServer_Click()
        {
#if DEBUG
            if (SimulateStartServerException) throw new InvalidOperationException("Intentional exception thrown for testing");
#endif
            StartServer(true);
        }

        private void ButtonStopServer_Click()
        {
#if DEBUG
            if (SimulateStopServerException) throw new InvalidOperationException("Intentional exception thrown for testing");
#endif
            Server.Stop();
        }

        private void ButtonRestartServer_Click()
        {
            Server.Restart();
        }

        private void ButtonClearLogs_Click(object sender, EventArgs e)
        {
            ClearCurrentLogView();
        }

        private void ButtonSaveLogs_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LogViewer.GetCurrentViewText()))
            {
                Logger.Warning("No logs to save!");
                return;
            }

            string initialDirectory;
            try
            {
                initialDirectory = PathExtensions.GetDirectoryInfo(Resources.LogsFolderPath).FullName;
            }
            catch
            {
                initialDirectory = null;
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"{PathExtensions.GetValidFileName(LogViewer.LogView, true)}.txt",
                Filter = "Text Files (*.txt)|*.txt",
                CheckPathExists = true,
                RestoreDirectory = true,
                InitialDirectory = initialDirectory,
            };

            var result = dialog.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                try
                {
                    // Get the text again in case any new logs were written
                    File.WriteAllText(dialog.FileName, LogViewer.GetCurrentViewText());
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Failed to write log file: {fileName}", dialog.FileName);
                }
            }
        }

        private void ButtonPlayerDetails_Click(object sender, EventArgs e)
        {
            if (!PlayersTable.TryGetSelectedRow<PlayerInfo>(out var row)) return;

            var form = FormProvider.GetForm<PlayerDetailsForm>();
            form.SetPlayerData(row.Entity);
            form.ShowDialog();
        }

        private void LinkCharacterNamesHelp_Click(object sender, EventArgs e)
        {
            OpenHelper.OpenWebAddress(Resources.UrlHelpCharacterNames);
        }

        private void ButtonRemovePlayer_Click(object sender, EventArgs e)
        {
            if (PlayersTable.TryGetSelectedRow<PlayerInfo>(out var row))
            {
                PlayerDataProvider.Remove(row.Entity);
                PlayersTable.RemoveSelectedRow();
            }
        }

        private void ServerNameField_Changed(object sender, string value)
        {
            RefreshFormStateForServer();
        }

        private void ShowPasswordField_Changed(object sender, bool value)
        {
            ServerPasswordField.HideValue = !value;
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                RefocusWindow();
            }
        }

        private void WorldSelectRadioExisting_Changed(object sender, bool value)
        {
            WorldSelectExistingNameField.Visible = value;

            if (value)
            {
                // When going back to the Existing World field, select the New Name if it's already an existing world
                WorldSelectExistingNameField.Value = WorldSelectNewNameField.Value;

                // Then, empty out the new name field
                WorldSelectNewNameField.Value = null;
            }
        }

        private void WorldSelectRadioNew_Changed(object sender, bool value)
        {
            WorldSelectNewNameField.Visible = value;
        }

        private void WorldSelectExistingNameField_EnabledChanged(object sender, EventArgs e)
        {
            RefreshWorldSelect();
        }

        private void TabPlayers_VisibleChanged(object sender, EventArgs e)
        {
            if (!TabPlayers.Visible) return;

            RefreshPlayersTable();
        }

        private void TabServerDetails_VisibleChanged()
        {
            if (!TabServerDetails.Visible) return;

            RefreshServerDetails();
            RefreshIpPorts();
        }

        private void LogViewSelectField_Changed(object sender, string viewName)
        {
            LogViewer.LogView = viewName;
        }

        private void ServerRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (TabPlayers.Visible) RefreshPlayersTable();
            if (TabServerDetails.Visible) RefreshServerDetails();
        }

        private void UpdateCheckTimer_Tick()
        {
            CheckForUpdates(false);
        }

        private void PlayersTable_SelectionChanged(object sender, EventArgs e)
        {
            var isSelected = PlayersTable.TryGetSelectedRow<PlayerInfo>(out var row);
            ButtonPlayerDetails.Enabled = isSelected;
            ButtonRemovePlayer.Enabled = isSelected && row.Entity.PlayerStatus == PlayerStatus.Offline;
        }

        private void StatusStripLabelRight_Click()
        {
            if (!StatusStripLabelRight.IsLink) return;

            CheckForUpdates(true);
        }

        private void TrayContextMenuServerName_Click(object sender, EventArgs e)
        {
            RefocusWindow();
        }

        private void WorldsListSettingsButton_Click()
        {
            string worldName;

            if (WorldSelectRadioNew.Value)
            {
                worldName = WorldSelectNewNameField.Value;

                if (string.IsNullOrWhiteSpace(worldName))
                {
                    MessageBox.Show(
                        "Please enter a new world name before changing modifier settings.",
                        "World Name Missing",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                worldName = WorldSelectExistingNameField.Value;

                if (string.IsNullOrWhiteSpace(worldName))
                {
                    MessageBox.Show(
                        "Unable to change modifier settings. No world is selected.",
                        "World Name Missing",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            var form = FormProvider.GetForm<WorldPreferencesForm>();
            form.SetWorld(worldName);
            form.ShowDialog();
        }

        private void WorldsListRefreshButton_Click()
        {
            RefreshWorldSelect();

            if (!WorldSelectRadioExisting.Value)
            {
                // Switch to the world list after refreshing it
                WorldSelectRadioExisting.Value = true;
            }
        }

        #endregion
    }
}
