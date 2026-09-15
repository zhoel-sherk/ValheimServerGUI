using Avalonia.Controls;
using ValheimServerGUI.Avalonia.ViewModels;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private readonly ValheimServer Server;
        private readonly IUserInteraction UserInteraction;
        private readonly IApplicationLogger Logger;

        /// <summary>
        /// True once a tray icon exists. When set, closing the window hides it to the tray instead
        /// of exiting, so a running server is never shut down by accident.
        /// </summary>
        public bool TrayEnabled { get; set; }

        /// <summary>
        /// Set by the tray's Exit action to allow the window to actually close.
        /// </summary>
        public bool AllowClose { get; set; }

        public MainWindow(
            ShellViewModel shellViewModel,
            ValheimServer server,
            IUserInteraction userInteraction,
            IApplicationLogger logger)
        {
            InitializeComponent();
            DataContext = shellViewModel;
            Server = server;
            UserInteraction = userInteraction;
            Logger = logger;

            Opened += (_, _) => Services.WindowsTheme.ApplyDarkTitleBar(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // Let OS shutdown / app-initiated shutdown proceed, and honour an explicit Exit.
            if (AllowClose || e.CloseReason != WindowCloseReason.WindowClosing)
            {
                base.OnClosing(e);
                return;
            }

            if (TrayEnabled)
            {
                // Standard behaviour: keep running in the tray so a live server is never stopped
                // just because the window was closed.
                e.Cancel = true;
                Hide();
                return;
            }

            // No tray available: fall back to confirming while the server is running.
            if (Server.IsAnyStatus(ServerStatus.Running, ServerStatus.Starting))
            {
                e.Cancel = true;
                _ = ConfirmCloseWithoutTrayAsync();
            }

            base.OnClosing(e);
        }

        private async System.Threading.Tasks.Task ConfirmCloseWithoutTrayAsync()
        {
            try
            {
                var confirmed = await UserInteraction.ConfirmAsync(
                    "Server is running",
                    "The Valheim server is still running. Stop it and close the window?");

                if (!confirmed) return;

                Server.Stop();
                AllowClose = true;
                Close();
            }
            catch (System.Exception e)
            {
                Logger.Error(e, "Failed to confirm window close");
            }
        }
    }
}
