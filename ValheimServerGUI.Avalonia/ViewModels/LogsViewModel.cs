using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
using ValheimServerGUI.Infrastructure;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Logs tab: shows the filtered server stdout or the application log, with save/clear and
    /// an open-logs-folder action (parity with the WinForms Logs tab).
    /// </summary>
    public partial class LogsViewModel : ObservableObject
    {
        public const string ServerView = "Server";
        public const string ApplicationView = "Application";

        private const int MaxEntries = 2000;

        private readonly IApplicationLogger Logger;
        private readonly IServerLogStream ServerLogStream;
        private readonly IUserInteraction UserInteraction;
        private readonly IPlatformIntegration PlatformIntegration;

        private readonly ObservableCollection<LogEntry> ServerEntries = new();
        private readonly ObservableCollection<LogEntry> ApplicationEntries = new();

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public IReadOnlyList<string> Views { get; } = new[] { ServerView, ApplicationView };

        [ObservableProperty]
        private string _selectedView = ServerView;

        /// <summary>
        /// Whether the view should follow new lines. The window turns this off when the user
        /// scrolls up to read something, and back on when they return to the bottom.
        /// </summary>
        [ObservableProperty]
        private bool _autoScroll = true;

        /// <summary>
        /// True while the whole buffer is being replayed into the visible list (view switch,
        /// clear). The window skips per-item scrolling during a replay, since firing a scroll per
        /// item would mean thousands of redundant relayouts.
        /// </summary>
        public bool IsBulkUpdate { get; private set; }

        /// <summary>Raised once a replay has finished, so the view can jump to the tail.</summary>
        public event EventHandler? BulkUpdateFinished;

        public LogsViewModel(
            IApplicationLogger logger,
            IServerLogStream serverLogStream,
            IUserInteraction userInteraction,
            IPlatformIntegration platformIntegration)
        {
            Logger = logger;
            ServerLogStream = serverLogStream;
            UserInteraction = userInteraction;
            PlatformIntegration = platformIntegration;

            Logger.LogReceived += OnApplicationLogReceived;
            ServerLogStream.LogReceived += OnServerLogReceived;

            // Flush the backlogs captured before the view was created.
            foreach (var entry in ServerLogStream.LogBuffer) ServerEntries.Add(ToServerEntry(entry));
            foreach (var entry in Logger.LogBuffer) ApplicationEntries.Add(ToApplicationEntry(entry));

            ShowSelectedView();
        }

        partial void OnSelectedViewChanged(string value) => ShowSelectedView();

        private void ShowSelectedView()
        {
            var source = SelectedView == ApplicationView ? ApplicationEntries : ServerEntries;

            ReplayInto(source);
        }

        /// <summary>
        /// Replaces the visible list with the contents of <paramref name="source"/> in one go.
        /// </summary>
        private void ReplayInto(System.Collections.IEnumerable source)
        {
            IsBulkUpdate = true;
            try
            {
                LogEntries.Clear();
                foreach (var entry in source)
                {
                    LogEntries.Add((LogEntry)entry);
                }
            }
            finally
            {
                IsBulkUpdate = false;
            }

            BulkUpdateFinished?.Invoke(this, EventArgs.Empty);
        }

        private void OnApplicationLogReceived(string message)
        {
            Dispatcher.UIThread.Post(() => Append(ApplicationEntries, ToApplicationEntry(message), SelectedView == ApplicationView));
        }

        private void OnServerLogReceived(string message)
        {
            Dispatcher.UIThread.Post(() => Append(ServerEntries, ToServerEntry(message), SelectedView == ServerView));
        }

        private static LogEntry ToServerEntry(string message)
            => new(message, LogSeverityClassifier.ClassifyServerLine(message));

        private static LogEntry ToApplicationEntry(string message)
            => new(message, LogSeverityClassifier.ClassifyApplicationLine(message));

        private void Append(ObservableCollection<LogEntry> buffer, LogEntry entry, bool isVisible)
        {
            buffer.Add(entry);
            if (buffer.Count > MaxEntries) buffer.RemoveAt(0);

            if (!isVisible) return;

            LogEntries.Add(entry);
            if (LogEntries.Count > MaxEntries) LogEntries.RemoveAt(0);
        }

        [RelayCommand]
        private void Clear()
        {
            if (SelectedView == ApplicationView)
            {
                ApplicationEntries.Clear();
            }
            else
            {
                ServerEntries.Clear();
                ServerLogStream.Clear();
            }

            IsBulkUpdate = true;
            try
            {
                LogEntries.Clear();
            }
            finally
            {
                IsBulkUpdate = false;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (LogEntries.Count == 0)
            {
                UserInteraction.ShowError("Save Logs", "No logs to save.");
                return;
            }

            var suggested = $"ValheimServerGUI-{SelectedView}Logs-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            var content = string.Join(Environment.NewLine, LogEntries.Select(entry => entry.Text));

            var path = await UserInteraction.SaveTextFileAsync("Save Logs", suggested, content);
            if (path != null)
            {
                Logger.Information("Saved {view} logs to {path}", SelectedView, path);
            }
        }

        [RelayCommand]
        private void OpenLogsFolder()
        {
            PlatformIntegration.OpenDirectory(AppSettings.LogsFolderPath);
        }
    }
}
