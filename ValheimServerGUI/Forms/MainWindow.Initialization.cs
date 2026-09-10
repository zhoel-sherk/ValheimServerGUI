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
        #region Initialization

        private void InitializeImages()
        {
            ImageList.AddImagesFromResourceFile(typeof(Resources));
        }

        private void InitializeServices()
        {
            Server.StatusChanged += this.BuildEventHandler<ServerStatus>(OnServerStatusChanged);
            Server.WorldSaved += this.BuildEventHandler<decimal>(OnWorldSaved);
            Server.InviteCodeReady += this.BuildEventHandler<string>(OnInviteCodeReady);

            PlayerDataProvider.EntityUpdated += this.BuildEventHandler<PlayerInfo>(OnPlayerUpdated);

            IpAddressProvider.ExternalIpChanged += this.BuildEventHandler<string>(IpAddressProvider_ExternalIpChanged);
            IpAddressProvider.InternalIpChanged += this.BuildEventHandler<string>(IpAddressProvider_InternalIpChanged);

            SoftwareUpdateProvider.UpdateCheckStarted += this.BuildEventHandler(SoftwareUpdateProvider_UpdateCheckStarted);
            SoftwareUpdateProvider.UpdateCheckFinished += this.BuildEventHandler<SoftwareUpdateEventArgs>(SoftwareUpdateProvider_UpdateCheckFinished);

            ServerPrefsProvider.PreferencesSaved += this.BuildEventHandler<List<ServerPreferences>>(OnPreferencesSaved);
        }

        private void InitializeFormEvents()
        {
            // MainWindow
            ProfileChanged += this.BuildEventHandler<string>(OnProfileChanged);

            // Menu items
            MenuItemFileNewWindow.Click += MenuItemFileNewWindow_Click;
            MenuItemFileNewProfile.Click += MenuItemFileNewProfile_Click;
            MenuItemFileSaveProfile.Click += MenuItemFileSaveProfile_Click;
            MenuItemFileSaveProfileAs.Click += MenuItemFileSaveProfileAs_Click;
            MenuItemFilePreferences.Click += MenuItemFilePreferences_Click;
            MenuItemFileDirectories.Click += MenuItemFileDirectories_Click;
            MenuItemFileOpenSettings.Click += MenuItemFileOpenSettings_Click;
            MenuItemFileClose.Click += MenuItemFileClose_Click;
            MenuItemHelpManual.Click += MenuItemHelpManual_Click;
            MenuItemHelpPortForwarding.Click += MenuItemHelpPortForwarding_Click;
            MenuItemHelpIssues.Click += MenuItemHelpIssues_Click;
            MenuItemHelpUpdates.Click += MenuItemHelpUpdates_Click;
            MenuItemHelpAbout.Click += MenuItemHelpAbout_Click;

            // Tray icon
            NotifyIcon.MouseClick += NotifyIcon_MouseClick;
            TrayContextMenuServerName.Click += TrayContextMenuServerName_Click;
            TrayContextMenuStart.Click += this.BuildEventHandler(ButtonStartServer_Click);
            TrayContextMenuRestart.Click += this.BuildEventHandler(ButtonRestartServer_Click);
            TrayContextMenuStop.Click += this.BuildEventHandler(ButtonStopServer_Click);
            TrayContextMenuClose.Click += MenuItemFileClose_Click;

            // Timers
            ServerRefreshTimer.Tick += ServerRefreshTimer_Tick;
            UpdateCheckTimer.Tick += this.BuildEventHandler(UpdateCheckTimer_Tick);

            // Tabs
            TabPlayers.VisibleChanged += TabPlayers_VisibleChanged;
            TabServerDetails.VisibleChanged += this.BuildEventHandler(TabServerDetails_VisibleChanged);

            // Buttons
            ButtonStartServer.Click += this.BuildEventHandler(ButtonStartServer_Click);
            ButtonRestartServer.Click += this.BuildEventHandler(ButtonRestartServer_Click);
            ButtonStopServer.Click += this.BuildEventHandler(ButtonStopServer_Click);
            ButtonClearLogs.Click += ButtonClearLogs_Click;
            ButtonSaveLogs.Click += ButtonSaveLogs_Click;
            LogsFolderOpenButton.PathFunction = () => Resources.LogsFolderPath;
            ButtonPlayerDetails.Click += ButtonPlayerDetails_Click;
            LinkCharacterNamesHelp.Click += LinkCharacterNamesHelp_Click;
            ButtonRemovePlayer.Click += ButtonRemovePlayer_Click;
            CopyButtonServerPassword.CopyFunction = () => ServerPasswordField.Value;
            WorldsListSettingsButton.ClickFunction = WorldsListSettingsButton_Click;
            WorldsListRefreshButton.RefreshFunction = WorldsListRefreshButton_Click;
            WorldsFolderOpenButton.PathFunction = () => GetServerOptionsFromFormState().SaveDataFolderPath;
            CopyButtonExternalIpAddress.CopyFunction = () => LabelExternalIpAddress.Value;
            CopyButtonInternalIpAddress.CopyFunction = () => LabelInternalIpAddress.Value;
            CopyButtonLocalIpAddress.CopyFunction = () => LabelLocalIpAddress.Value;
            CopyButtonInviteCode.CopyFunction = () => LabelInviteCode.Value;
            ServerExePathOpenButton.PathFunction = () => ServerExePathField.Value;
            ServerSaveDataPathOpenButton.PathFunction = () => ServerSaveDataFolderPathField.Value;
            StatusStripLabelRight.Click += this.BuildEventHandler(StatusStripLabelRight_Click);

            // Form fields
            ServerNameField.ValueChanged += ServerNameField_Changed;
            ShowPasswordField.ValueChanged += ShowPasswordField_Changed;
            WorldSelectRadioExisting.ValueChanged += WorldSelectRadioExisting_Changed;
            WorldSelectRadioNew.ValueChanged += WorldSelectRadioNew_Changed;
            WorldSelectExistingNameField.EnabledChanged += WorldSelectExistingNameField_EnabledChanged;
            LogViewSelectField.ValueChanged += LogViewSelectField_Changed;
            PlayersTable.SelectionChanged += PlayersTable_SelectionChanged;
        }

        private void InitializeFormFields()
        {
            // Write message backlog to application log view...
            foreach (var message in Logger.LogBuffer)
            {
                LogViewer.AddLogToView(message, LogViews.Application);
            }
            // ...then write all new messages to that log view.
            Logger.LogReceived += this.BuildActionHandler<string>(OnApplicationLogReceived);

            LogViewSelectField.DataSource = new[] { LogViews.Server, LogViews.Application };
            LogViewSelectField.Value = LogViews.Server;
            ServerExePathField.ConfigureFileDialog(dialog => dialog.Filter = "Applications (*.exe)|*.exe");

            RefreshFormFields();
            RefreshModsTab();
            RefreshBackups();
        }

        private void InitializeStartupPrefs()
        {
            // If StartProfile was set by SplashForm before this window was shown, then
            // load that server profile up now, and start the server is settings indicate that.
            if (StartProfile != null)
            {
                var serverPrefs = SetFormStateFromPrefs(StartProfile);
                if (serverPrefs != null && StartServerAutomatically)
                {
                    StartServer(false);
                }
                else
                {
                    OnServerStatusChanged(ServerStatus.Stopped);
                }
            }
            else
            {
                // No server to start, mock a "stopped" event to initialize the form
                OnServerStatusChanged(ServerStatus.Stopped);
            }
        }

        private void InitializeIpAddresses()
        {
            var internalIp = IpAddressProvider.InternalIpAddress;
            var externalIp = IpAddressProvider.ExternalIpAddress;

            LabelInternalIpAddress.Value = internalIp ?? IpLoadingText;
            LabelExternalIpAddress.Value = externalIp ?? IpLoadingText;

            if (internalIp == null) IpAddressProvider.LoadInternalIpAddressAsync();
            if (externalIp == null) IpAddressProvider.LoadExternalIpAddressAsync();

            RefreshIpPorts();
        }

        private void InitializePlayerData()
        {
            PlayersTable.AddRowBinding<PlayerInfo>(row =>
            {
                row.AddCellBinding(ColumnPlayerName.Index, GetPlayerDisplayName);
                row.AddCellBinding(ColumnPlayerStatus.Index, p => p.PlayerStatus);
                row.AddCellBinding(ColumnPlayerUpdated.Index, p => new TimeAgo(p.LastStatusChange));
            });

            foreach (var playerInfo in PlayerDataProvider.Data)
            {
                SetPlayerStatus(playerInfo);
            }
        }

        #endregion
    }
}
