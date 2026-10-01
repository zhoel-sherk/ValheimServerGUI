using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
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

            WireUpLogAutoScroll(shellViewModel);
        }

        /// <summary>
        /// Keeps the log view pinned to the newest line, unless the user has scrolled up to read
        /// something. Scrolling back to the bottom re-arms it.
        /// </summary>
        private void WireUpLogAutoScroll(ShellViewModel shellViewModel)
        {
            var logs = shellViewModel.Logs;

            logs.LogEntries.CollectionChanged += (_, _) =>
            {
                if (logs.IsBulkUpdate || !logs.AutoScroll) return;

                ScrollLogToEnd();
            };

            // A view switch or clear replays the whole buffer; scroll once, at the end of it.
            logs.BulkUpdateFinished += (_, _) =>
            {
                if (logs.AutoScroll) ScrollLogToEnd();
            };

            // The scroll viewer only exists once the template is applied and the items panel is
            // built, so poll for it until it turns up. The flag is essential: re-subscribing from
            // inside the handler would add a fresh delegate on every layout pass and the handlers
            // would multiply exponentially.
            var scrollWatcherAttached = false;

            void AttachScrollViewer()
            {
                var scroller = LogList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (scroller != null)
                {
                    scrollWatcherAttached = true;

                    // Only re-arm while the user is parked at the bottom, so following the tail
                    // never yanks the view away from something they scrolled up to read.
                    scroller.ScrollChanged += (_, _) =>
                    {
                        var atBottom = scroller.Offset.Y + scroller.Viewport.Height >= scroller.Extent.Height - 4;
                        if (atBottom != logs.AutoScroll) logs.AutoScroll = atBottom;
                    };
                }
            }

            void WatchForScrollViewer(object? sender, EventArgs e)
            {
                if (scrollWatcherAttached) return;

                AttachScrollViewer();
                if (scrollWatcherAttached) LayoutUpdated -= WatchForScrollViewer;
            }

            AttachScrollViewer();
            if (!scrollWatcherAttached) LayoutUpdated += WatchForScrollViewer;

            // The list has no height until laid out, so the first entries can arrive too early to scroll.
            LogList.SizeChanged += (_, _) =>
            {
                if (logs.AutoScroll && !logs.IsBulkUpdate) ScrollLogToEnd();
            };
        }

        private void ScrollLogToEnd()
        {
            if (LogList.ItemCount > 0)
            {
                LogList.ScrollIntoView(LogList.ItemCount - 1);
            }
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
