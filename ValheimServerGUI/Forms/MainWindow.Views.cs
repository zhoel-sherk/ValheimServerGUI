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
        #region View Setters

        private void ClearCurrentLogView()
        {
            LogViewer.ClearLogView(LogViewer.LogView);
        }

        private void SetInviteCode(string inviteCode, bool copyable = true)
        {
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                LabelInviteCode.Value = "N/A";
                CopyButtonInviteCode.Visible = false;
                return;
            }

            LabelInviteCode.Value = inviteCode;
            CopyButtonInviteCode.Visible = copyable;
        }

        private void SetPlayerStatus(PlayerInfo player)
        {
            var playerRows = PlayersTable
                .GetRowsWithType<PlayerInfo>()
                .Where(r => r.Entity.Platform == player.Platform && r.Entity.PlayerId == player.PlayerId);

            var playerRow = playerRows.FirstOrDefault(p => p.Entity.Key == player.Key) ?? PlayersTable.AddRowFromEntity(player);
            if (playerRow == null) return;

            // Update styles based on player status and platform
            var platformImageIndex = -1;
            var color = PlayersTable.ForeColor;

            switch (player.Platform)
            {
                case PlayerPlatforms.Steam:
                    platformImageIndex = GetImageIndex(nameof(Resources.Steam_16x));
                    break;
                case PlayerPlatforms.Xbox:
                    platformImageIndex = GetImageIndex(nameof(Resources.XboxLive_16x));
                    break;
            }

            switch (player.PlayerStatus)
            {
                case PlayerStatus.Offline:
                    color = Color.Gray;
                    break;
            }

            playerRow.ImageIndex = platformImageIndex;
            playerRow.ForeColor = color;
        }

        private void SetStatusTextLeft(string message, Image icon)
        {
            StatusStripLabelLeft.Text = message;
            StatusStripLabelLeft.Image = icon;
        }

        private void SetStatusTextRight(string message, Image icon, bool isLink)
        {
            StatusStripLabelRight.Text = message;
            StatusStripLabelRight.Image = icon;
            StatusStripLabelRight.IsLink = isLink;
        }

        #endregion

        #region View Refreshers

        private void RefreshFormFields()
        {
            RefreshProfileList();
            RefreshWorldSelect();
            RefreshFormStateForServer();
        }

        private void RefreshFormStateForServer()
        {
            // Only allow form field changes when the server is stopped
            bool allowServerChanges = Server.Status == ServerStatus.Stopped;

            ServerNameField.Enabled = allowServerChanges;
            ServerPortField.Enabled = allowServerChanges;
            ServerPasswordField.Enabled = allowServerChanges;
            WorldSelectRadioNew.Enabled = allowServerChanges;
            WorldSelectRadioExisting.Enabled = allowServerChanges;
            WorldSelectExistingNameField.Enabled = allowServerChanges;
            WorldSelectNewNameField.Enabled = allowServerChanges;
            CommunityServerField.Enabled = allowServerChanges;
            ServerCrossplayField.Enabled = allowServerChanges;
            ServerSaveIntervalField.Enabled = allowServerChanges;
            ServerBackupsField.Enabled = allowServerChanges;
            ServerShortBackupIntervalField.Enabled = allowServerChanges;
            ServerLongBackupIntervalField.Enabled = allowServerChanges;
            ServerAutoStartField.Enabled = allowServerChanges;
            ServerAdditionalArgsField.Enabled = allowServerChanges;
            ServerExePathField.Enabled = allowServerChanges;
            ServerSaveDataFolderPathField.Enabled = allowServerChanges;
            ServerLogFileField.Enabled = allowServerChanges;

            MenuItemFileNewProfile.Enabled = allowServerChanges;
            MenuItemFileLoadProfile.Enabled = allowServerChanges;

            ButtonStartServer.Enabled = Server.CanStart;
            ButtonRestartServer.Enabled = Server.CanRestart;
            ButtonStopServer.Enabled = Server.CanStop;

            // Tray items are enabled based on their button equivalents
            TrayContextMenuStart.Enabled = ButtonStartServer.Enabled;
            TrayContextMenuRestart.Enabled = ButtonRestartServer.Enabled;
            TrayContextMenuStop.Enabled = ButtonStopServer.Enabled;

            TrayContextMenuServerName.Image = ServerStatusIconMap[Server.Status];
        }

        private void RefreshIpPorts()
        {
            const string ipExpr = @"^([\d]{1,3}\.[\d]{1,3}\.[\d]{1,3}\.[\d]{1,3})";

            var fields = new[] { LabelExternalIpAddress, LabelInternalIpAddress, LabelLocalIpAddress };
            var destPort = ServerPortField.Value;
            var isDefaultPort = destPort.ToString() == Resources.DefaultServerPort;

            foreach (var field in fields)
            {
                if (field.Value == null || field.Value == IpLoadingText) continue; // Don't try to modify loading text

                var ipMatch = Regex.Match(field.Value, ipExpr);
                var captures = (ipMatch.Groups as IEnumerable<Group>).Skip(1).Select(g => g.ToString()).ToArray();

                if (captures.Length == 0) continue; // Quit if we can't extract the IP address

                var ip = captures[0];
                field.Value = isDefaultPort ? ip : $"{ip}:{destPort}"; // Only append the port if it's not the default
            }
        }

        private void RefreshProfileList()
        {
            MenuItemFileLoadProfile.Enabled = false;
            MenuItemFileLoadProfile.DropDownItems.Clear();

            MenuItemFileRemoveProfile.Enabled = false;
            MenuItemFileRemoveProfile.DropDownItems.Clear();

            var prefs = ServerPrefsProvider.LoadPreferences()
                .OrderByDescending(p => p.LastSaved);
            if (prefs == null || !prefs.Any()) return;

            MenuItemFileLoadProfile.Enabled = true;
            MenuItemFileRemoveProfile.Enabled = true;

            foreach (var pref in prefs)
            {
                MenuItemFileLoadProfile.DropDownItems.Add(
                    pref.ProfileName,
                    null,
                    MenuItemFileLoadProfileItem_Click);
                MenuItemFileRemoveProfile.DropDownItems.Add(
                    pref.ProfileName,
                    null,
                    MenuItemFileRemoveProfileItem_Click);
            }
        }

        private void RefreshPlayersTable()
        {
            foreach (var row in PlayersTable.GetRowsWithType<PlayerInfo>())
            {
                row.RefreshValues();
            }
        }

        private void RefreshServerDetails()
        {
            if (Server.Status == ServerStatus.Running && ServerUptimeTimer != null)
            {
                var elapsed = ServerUptimeTimer.Elapsed;
                var days = elapsed.Days;
                var timestr = elapsed.ToServerElapsedFormat();

                if (days == 1) timestr = $"1 day + {timestr}";
                else if (days > 1) timestr = $"{days} days + {timestr}";

                LabelSessionDuration.Value = timestr;
            }
        }

        private void RefreshWorldSelect()
        {
            try
            {
                // Refresh the existing worlds list, then re-select whatever was originally selected
                var selectedWorld = WorldSelectExistingNameField.Value;
                var options = GetServerOptionsFromFormState();
                var worlds = options.GetValidatedSaveDataFolder().GetWorldNames();

                WorldSelectExistingNameField.DataSource = worlds;
                WorldSelectExistingNameField.DropdownEnabled = worlds.Any();
                WorldSelectExistingNameField.Value = selectedWorld;
            }
            catch (Exception e)
            {
                // Show no worlds if something goes wrong
                WorldSelectExistingNameField.DataSource = null;
                WorldSelectExistingNameField.DropdownEnabled = false;
                Logger.Error("Error refreshing world select: {message}", e.Message);
            }
        }

        private void RefreshCurrentProfileDisplayed()
        {
            if (CurrentProfile == null)
            {
                Text = Resources.ApplicationTitle;
                NotifyIcon.Text = "ValheimServerGUI";
                TrayContextMenuServerName.Text = "No Profile Selected";
            }
            else
            {
                Text = $"{Resources.ApplicationTitle} - {CurrentProfile}";
                NotifyIcon.Text = $"ValheimServerGUI - {CurrentProfile}";
                TrayContextMenuServerName.Text = $"Profile: {CurrentProfile}";
            }
        }

        #endregion
    }
}
