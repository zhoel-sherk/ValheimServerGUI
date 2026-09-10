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
        #region Service Events

        private void OnApplicationLogReceived(string message)
        {
            LogViewer.AddLogToView(message, LogViews.Application);
        }

        private void OnServerLogReceived(string message)
        {
            LogViewer.AddLogToView(message, LogViews.Server);
        }

        private void OnServerStatusChanged(ServerStatus status)
        {
            SetStatusTextLeft(status.ToString(), ServerStatusIconMap[status]);

            RefreshFormStateForServer();

            if (status == ServerStatus.Running && WorldSelectRadioNew.Value)
            {
                // Once a "new world" starts running, switch back to the Existing Worlds screen
                // and select the newly created world
                RefreshWorldSelect();
                var worldName = WorldSelectNewNameField.Value;
                WorldSelectRadioExisting.Value = true;
                WorldSelectExistingNameField.Value = worldName;
            }

            if (status == ServerStatus.Running)
            {
                ServerUptimeTimer.Restart();
            }
            else
            {
                if (ServerUptimeTimer.IsRunning) ServerUptimeTimer.Stop();
            }

            // Files are locked while the server is running, so keep the mods tab in sync
            if (TabMods.Visible) RefreshModsTab();

            if (status == ServerStatus.Stopped)
            {
                // Invite codes are only good for the session, clear it out when it's done
                SetInviteCode(null);
            }
            else if (status == ServerStatus.Starting && ServerCrossplayField.Value)
            {
                SetInviteCode("Loading...", false);
            }
        }

        private void OnPlayerUpdated(PlayerInfo player)
        {
            SetPlayerStatus(player);
        }

        private void OnWorldSaved(decimal duration)
        {
            LabelLastWorldSave.Value = $"{DateTime.Now:G} ({duration:F}ms)";

            if (WorldSaveTimes.Count >= 10) WorldSaveTimes.Dequeue();
            WorldSaveTimes.Enqueue(duration);

            var average = WorldSaveTimes.Average();
            LabelAverageWorldSave.Value = $"{average:F}ms";

            // A world save may also produce an automatic backup, so keep the backups tab fresh
            if (TabBackups.Visible) RefreshBackups();
        }

        private void OnInviteCodeReady(string inviteCode)
        {
            SetInviteCode(inviteCode);
        }

        private void IpAddressProvider_ExternalIpChanged(string ip)
        {
            LabelExternalIpAddress.Value = ip;
            RefreshIpPorts();
        }

        private void IpAddressProvider_InternalIpChanged(string ip)
        {
            LabelInternalIpAddress.Value = ip;
            RefreshIpPorts();
        }

        private void SoftwareUpdateProvider_UpdateCheckStarted()
        {
            SetStatusTextRight("Checking for updates...", Resources.Loading_Blue_16x, false);
        }

        private void SoftwareUpdateProvider_UpdateCheckFinished(SoftwareUpdateEventArgs e)
        {
            string manualCheckMessage;
            MessageBoxIcon manualCheckIcon;

            if (!e.IsSuccessful)
            {
                SetStatusTextRight($"Update check failed", Resources.StatusCriticalError_16x, true);

                var exception = e.Exception.GetPrimaryException();
                manualCheckMessage = $"Update check failed: {exception.Message}.";
                manualCheckIcon = MessageBoxIcon.Error;
            }
            else
            {
                var versionCompareResult = AssemblyHelper.CompareVersion(e.LatestVersion);

                if (versionCompareResult > 0)
                {
                    SetStatusTextRight($"Update available ({e.LatestVersion})", Resources.StatusWarning_16x, true);
                    manualCheckMessage = "A newer version of ValheimServerGUI is available.";
                    manualCheckIcon = MessageBoxIcon.Warning;
                }
                else if (versionCompareResult == 0)
                {
                    SetStatusTextRight($"Up to date ({e.LatestVersion})", Resources.StatusOK_16x, false);
                    manualCheckMessage = "You are running the latest version of ValheimServerGUI.";
                    manualCheckIcon = MessageBoxIcon.Question;
                }
                else if (versionCompareResult == -1)
                {
                    SetStatusTextRight($"Pre-release build ({AssemblyHelper.GetApplicationVersion()})", Resources.StatusOK_16x, false);
                    manualCheckMessage = "You are currently running a pre-release version of ValheimServerGUI. " +
                        $"The latest stable version is ({e.LatestVersion}).";
                    manualCheckIcon = MessageBoxIcon.Question;
                }
                else
                {
                    SetStatusTextRight($"Unable to parse version ({e.LatestVersion})", Resources.StatusCriticalError_16x, true);
                    manualCheckMessage = $"Update check failed: Unable to parse version ({e.LatestVersion}).";
                    manualCheckIcon = MessageBoxIcon.Error;
                }
            }

            if (e.IsManualCheck)
            {
                var result = MessageBox.Show(
                    $"{manualCheckMessage}{Environment.NewLine}Would you like to go to the download page?",
                    "Check for Updates",
                    MessageBoxButtons.YesNo,
                    manualCheckIcon);

                if (result == DialogResult.Yes)
                {
                    OpenHelper.OpenWebAddress(Resources.UrlUpdates);
                }
            }
        }

        private void OnPreferencesSaved(List<ServerPreferences> prefs)
        {
            RefreshProfileList();
        }

        #endregion

        #region Form Links

        private void ShowPreferencesForm()
        {
            FormProvider.GetForm<PreferencesForm>().ShowDialog();
            RefreshFormFields();
        }

        private void ShowDirectoriesForm()
        {
            FormProvider.GetForm<DirectoriesForm>().ShowDialog();
            RefreshFormFields();
        }

        #endregion
    }
}
