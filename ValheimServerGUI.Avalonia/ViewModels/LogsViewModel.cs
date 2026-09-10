using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Logs tab (Phase 2 step 6): displays the application and server log streams.
    /// </summary>
    public partial class LogsViewModel : ObservableObject
    {
        private readonly IApplicationLogger Logger;

        public ObservableCollection<string> LogEntries { get; } = new();

        [ObservableProperty]
        private bool _isServerLog;

        public LogsViewModel(IApplicationLogger logger)
        {
            Logger = logger;

            Logger.LogReceived += OnLogReceived;

            // Flush the backlog captured before the view was created.
            foreach (var entry in Logger.LogBuffer)
            {
                LogEntries.Add(entry);
            }
        }

        private void OnLogReceived(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Add(message);

                // Bound log lists: cap to prevent unbounded memory growth.
                if (LogEntries.Count > 2000)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }

        [RelayCommand]
        private void Clear()
        {
            LogEntries.Clear();
        }
    }
}