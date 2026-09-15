using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Platform;
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

        private readonly ObservableCollection<string> ServerEntries = new();
        private readonly ObservableCollection<string> ApplicationEntries = new();

        public ObservableCollection<string> LogEntries { get; } = new();

        public IReadOnlyList<string> Views { get; } = new[] { ServerView, ApplicationView };

        [ObservableProperty]
        private string _selectedView = ServerView;

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
            foreach (var entry in ServerLogStream.LogBuffer) ServerEntries.Add(entry);
            foreach (var entry in Logger.LogBuffer) ApplicationEntries.Add(entry);

            ShowSelectedView();
        }

        partial void OnSelectedViewChanged(string value) => ShowSelectedView();

        private void ShowSelectedView()
        {
            var source = SelectedView == ApplicationView ? ApplicationEntries : ServerEntries;

            LogEntries.Clear();
            foreach (var entry in source)
            {
                LogEntries.Add(entry);
            }
        }

        private void OnApplicationLogReceived(string message)
        {
            Dispatcher.UIThread.Post(() => Append(ApplicationEntries, message, SelectedView == ApplicationView));
        }

        private void OnServerLogReceived(string message)
        {
            Dispatcher.UIThread.Post(() => Append(ServerEntries, message, SelectedView == ServerView));
        }

        private void Append(ObservableCollection<string> buffer, string message, bool isVisible)
        {
            buffer.Add(message);
            if (buffer.Count > MaxEntries) buffer.RemoveAt(0);

            if (!isVisible) return;

            LogEntries.Add(message);
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

            LogEntries.Clear();
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
            var content = string.Join(Environment.NewLine, LogEntries);

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
