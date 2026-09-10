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
    public partial class MainWindow : Form
    {
#if DEBUG
        private static readonly bool SimulateConstructorException = false;
        private static readonly bool SimulateStartServerException = false;
        private static readonly bool SimulateStopServerException = false;
#endif
        private string _currentProfile;
        public string CurrentProfile
        {
            get => _currentProfile;
            set
            {
                _currentProfile = value;
                ProfileChanged?.Invoke(this, _currentProfile);
            }
        }
        public EventHandler<string> ProfileChanged;
        public int SplashIndex { get; set; }

        /// <summary>
        /// Load this profile into the form when the window is first loaded.
        /// </summary>
        public string StartProfile { get; set; }

        /// <summary>
        /// If true, start the server from the StartProfile settings as soon
        /// as the window is loaded.
        /// </summary>
        public bool StartServerAutomatically { get; set; }

        private static readonly string NL = Environment.NewLine;
        private const string IpLoadingText = "Loading...";

        private readonly Stopwatch ServerUptimeTimer = new();
        private readonly Queue<decimal> WorldSaveTimes = new();
        private readonly Dictionary<ServerStatus, Image> ServerStatusIconMap = new()
        {
            { ServerStatus.Stopped, Resources.StatusPause_grey_16x },
            { ServerStatus.Starting, Resources.UnsyncedCommits_16x_Horiz },
            { ServerStatus.Running, Resources.StatusRun_16x },
            { ServerStatus.Stopping, Resources.UnsyncedCommits_16x_Horiz },
        };

        private readonly IFormProvider FormProvider;
        private readonly IUserPreferencesProvider UserPrefsProvider;
        private readonly IServerPreferencesProvider ServerPrefsProvider;
        private readonly IWorldPreferencesProvider WorldPrefsProvider;
        private readonly IPlayerDataRepository PlayerDataProvider;
        private readonly ValheimServer Server;
        private readonly IApplicationLogger Logger;
        private readonly IIpAddressProvider IpAddressProvider;
        private readonly ISoftwareUpdateProvider SoftwareUpdateProvider;
        private readonly IBepInExManager BepInExManager;
        private readonly IValheimPlusManager ValheimPlusManager;
        private readonly IBackupService BackupService;

        private TabPage TabMods;
        private TabPage TabBackups;
        private LabelField BepInExStatusField;
        private Button ButtonInstallBepInEx;
        private Button ButtonBepInExFromFile;
        private Button ButtonCheckBepInExUpdate;
        private LabelField ValheimPlusStatusField;
        private Button ButtonInstallValheimPlus;
        private Button ButtonValheimPlusFromFile;
        private Button ButtonOpenValheimPlusConfig;
        private LabelField BackupsStatusField;
        private ListView BackupsListView;
        private Button ButtonRefreshBackups;
        private Button ButtonOpenBackupsFolder;
        private bool ModOperationRunning;

        public MainWindow(
            IFormProvider formProvider,
            IUserPreferencesProvider userPrefsProvider,
            IServerPreferencesProvider serverPrefsProvider,
            IWorldPreferencesProvider worldPrefsProvider,
            IPlayerDataRepository playerDataProvider,
            ValheimServer server,
            IApplicationLogger appLogger,
            IIpAddressProvider ipAddressProvider,
            ISoftwareUpdateProvider softwareUpdateProvider,
            IBepInExManager bepInExManager,
            IValheimPlusManager valheimPlusManager,
            IBackupService backupService)
        {
#if DEBUG
            if (SimulateConstructorException) throw new InvalidOperationException("Intentional exception thrown for testing");
#endif
            FormProvider = formProvider;
            UserPrefsProvider = userPrefsProvider;
            ServerPrefsProvider = serverPrefsProvider;
            WorldPrefsProvider = worldPrefsProvider;
            PlayerDataProvider = playerDataProvider;
            Server = server;
            Logger = appLogger;
            IpAddressProvider = ipAddressProvider;
            SoftwareUpdateProvider = softwareUpdateProvider;
            BepInExManager = bepInExManager;
            ValheimPlusManager = valheimPlusManager;
            BackupService = backupService;

            InitializeComponent(); // WinForms generated code, always first
            this.AddApplicationIcon();
            InitializeImages();
            BuildModsTab();
            BuildBackupsTab();
            InitializeServices();
            InitializeFormEvents();
            InitializeFormFields(); // Display data back to user, always last
        }

        #region MainWindow Events

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            InitializeIpAddresses();
            InitializePlayerData();
            InitializeStartupPrefs();
            CheckFilePaths();
            NotifyIcon.Visible = true;

            Logger.Information("Valheim Server GUI v{version} - Loaded OK", AssemblyHelper.GetApplicationVersion());
        }

        private void OnProfileChanged(string _)
        {
            RefreshCurrentProfileDisplayed();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
            }
            else
            {
                ShowInTaskbar = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (Server.IsAnyStatus(ServerStatus.Starting, ServerStatus.Running))
                {
                    // Server is still running, prompt the user to confirm they want to stop it
                    var result = MessageBox.Show(
                        "The Valheim server is still running. Do you want to stop the server " +
                        "and close this window?",
                        "Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        Server.Stop();
                        CloseWindowOnServerStopped();
                    }

                    // Cancel the form close regardless of the user's choice
                    e.Cancel = true;
                }
                else if (Server.IsAnyStatus(ServerStatus.Stopping))
                {
                    var result = MessageBox.Show(
                        "The Valheim server is currently shutting down. Close anyway?" + Environment.NewLine +
                        "This could result in a loss of save data!",
                        "Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.No)
                    {
                        // Cancel the form close event, keep running the app
                        e.Cancel = true;
                        return;
                    }

                    // Otherwise, close out this window anyway
                }

                // Otherwise, the server is stopped, so we can safely close out this window
            }
            else if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                // Try to stop the server, and keep the app running until we're sure it's stopped
                Server.Stop();
                CloseWindowOnServerStopped();
                e.Cancel = true;
            }
            else
            {
                // Try to stop the server, but don't wait around for the results
                Server.Stop();
            }
        }

        #endregion

        #region Helper Methods

        private int GetImageIndex(string key)
        {
            return ImageList.Images.IndexOfKey(key);
        }

        private string GetPlayerDisplayName(PlayerInfo player)
        {
            // Show the last 4 digits of the player's platform ID if their name is not yet known
            var name = player.PlayerName ?? $"[...{player.PlayerId[^4..]}]";

            if (!string.IsNullOrWhiteSpace(player.LastStatusCharacter))
            {
                name += $" ({player.LastStatusCharacter})";
            }

            return name;
        }

        #endregion
    }
}
