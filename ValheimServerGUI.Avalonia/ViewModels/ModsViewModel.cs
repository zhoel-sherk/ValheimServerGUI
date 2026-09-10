using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ValheimServerGUI.Game;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Infrastructure;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Mods &amp; backups tab (Phase 2 step 7): BepInEx, Valheim Plus and world backup status.
    /// </summary>
    public partial class ModsViewModel : ObservableObject
    {
        private readonly IBepInExManager BepInExManager;
        private readonly IValheimPlusManager ValheimPlusManager;
        private readonly IBackupService BackupService;
        private readonly ServerControlsViewModel ServerControls;
        private readonly IApplicationLogger Logger;

        public ObservableCollection<BackupRowViewModel> Backups { get; } = new();

        [ObservableProperty]
        private string? _bepInExStatus;

        [ObservableProperty]
        private string? _valheimPlusStatus;

        [ObservableProperty]
        private string? _backupsStatus;

        [ObservableProperty]
        private bool _isBusy;

        public ModsViewModel(
            IBepInExManager bepInExManager,
            IValheimPlusManager valheimPlusManager,
            IBackupService backupService,
            ServerControlsViewModel serverControls,
            IApplicationLogger logger)
        {
            BepInExManager = bepInExManager;
            ValheimPlusManager = valheimPlusManager;
            BackupService = backupService;
            ServerControls = serverControls;
            Logger = logger;

            // Refresh only when the paths that determine the server/mods folder change.
            ServerControls.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ServerControls.ServerExePath)
                    || e.PropertyName == nameof(ServerControls.SaveDataFolderPath))
                {
                    Refresh();
                }
            };
        }

        private string? GetServerFolder()
        {
            var exePath = ServerControls.ServerExePath;
            if (string.IsNullOrWhiteSpace(exePath)) return null;

            return Path.GetDirectoryName(Path.GetFullPath(exePath));
        }

        [RelayCommand]
        private void Refresh()
        {
            if (IsBusy) return;

            var serverFolder = GetServerFolder();
            if (string.IsNullOrWhiteSpace(serverFolder) || !Directory.Exists(serverFolder))
            {
                BepInExStatus = "Server folder not available";
                ValheimPlusStatus = "Server folder not available";
                BackupsStatus = "No server folder selected";
                Backups.Clear();
                return;
            }

            var bepInEx = BepInExManager.GetStatus(serverFolder);
            BepInExStatus = FormatModStatus("BepInEx", bepInEx);

            var valheimPlus = ValheimPlusManager.GetStatus(serverFolder);
            ValheimPlusStatus = FormatModStatus("Valheim Plus", valheimPlus);

            RefreshBackups(serverFolder);
        }

        private void RefreshBackups(string serverFolder)
        {
            try
            {
                var options = ServerControls.BuildOptions();
                var saveFolder = options.GetValidatedSaveDataFolder();
                var status = BackupService.GetStatus(saveFolder, options.Backups, options.BackupShort);

                BackupsStatus = status.Summary;
                Backups.Clear();
                foreach (var backup in status.Backups)
                {
                    Backups.Add(new BackupRowViewModel(backup));
                }
            }
            catch (Exception e)
            {
                Logger.Error("Error refreshing backups: {message}", e.Message);
                BackupsStatus = "Unable to load backups";
            }
        }

        private static string FormatModStatus(string modName, ModStatus status)
        {
            if (!status.IsInstalled) return $"{modName}: Not installed";

            var version = status.PackageVersion ?? status.Version;
            if (status.UpdateAvailable)
            {
                return $"{modName}: v{version} (update available: v{status.LatestVersion})";
            }

            return $"{modName}: v{version}";
        }

        [RelayCommand]
        private async Task InstallBepInExAsync()
        {
            await RunModOperationAsync(
                "Installing BepInEx...",
                (folder, onProgress) => BepInExManager.InstallAsync(folder, onProgress),
                "BepInEx install failed: {message}",
                setStatus: s => BepInExStatus = s);
        }

        [RelayCommand]
        private async Task InstallValheimPlusAsync()
        {
            await RunModOperationAsync(
                "Installing Valheim Plus...",
                (folder, onProgress) => ValheimPlusManager.InstallAsync(folder, onProgress),
                "Valheim Plus install failed: {message}",
                setStatus: s => ValheimPlusStatus = s);
        }

        private async Task RunModOperationAsync(
            string progressMessage,
            Func<string, Action<string>, Task<ModStatus>> operation,
            string errorTemplate,
            Action<string> setStatus)
        {
            if (IsBusy) return;

            var serverFolder = GetServerFolder();
            if (string.IsNullOrWhiteSpace(serverFolder) || !Directory.Exists(serverFolder))
            {
                setStatus("Server folder not available");
                return;
            }

            IsBusy = true;
            try
            {
                setStatus(progressMessage);
                await operation(serverFolder, setStatus);
                Refresh();
            }
            catch (Exception e)
            {
                Logger.Error(errorTemplate, e.Message);
                setStatus($"Error: {e.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class BackupRowViewModel
    {
        private readonly BackupInfo Backup;

        public string Name => Backup.Name;
        public string Date => Backup.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string Size => FormatSize(Backup.SizeBytes);

        public BackupRowViewModel(BackupInfo backup)
        {
            Backup = backup;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}