using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Game.Mods
{
    public interface IValheimPlusManager
    {
        ModStatus GetStatus(string serverFolder);

        bool IsBepInExInstalled(string serverFolder);

        string GetConfigPath(string serverFolder);

        Task<ModStatus> InstallAsync(string serverFolder, Action<string> onProgress = null);

        Task<ModStatus> InstallFromFileAsync(string serverFolder, string zipPath, Action<string> onProgress = null);

        Task<ModStatus> CheckForUpdateAsync(string serverFolder);
    }

    /// <summary>
    /// Installs &amp; updates the Grantapher fork of Valheim Plus for a Valheim dedicated server.
    /// </summary>
    public class ValheimPlusManager : IValheimPlusManager
    {
        private const string PluginFolder = "BepInEx/plugins";
        private const string ConfigFolder = "BepInEx/config";
        private const string UserConfigFileName = "org.bepinex.plugins.valheim_plus.cfg";
        private const string BundledConfigFileName = "valheim_plus.cfg";

        private static readonly string[] PluginFileNames = { "ValheimPlus.dll", "ValheimPlusGrantapher.dll" };
        private static readonly string[] GameLayoutProbes =
        {
            "BepInEx/plugins/ValheimPlus.dll",
            "BepInEx/plugins/ValheimPlusGrantapher.dll",
            "BepInEx/config/org.bepinex.plugins.valheim_plus.cfg",
        };

        private readonly IModSourceClient ModSourceClient;
        private readonly IBepInExManager BepInExManager;
        private readonly IApplicationLogger Logger;

        public ValheimPlusManager(
            IModSourceClient modSourceClient,
            IBepInExManager bepInExManager,
            IApplicationLogger logger)
        {
            ModSourceClient = modSourceClient;
            BepInExManager = bepInExManager;
            Logger = logger;
        }

        public bool IsBepInExInstalled(string serverFolder)
            => BepInExManager.GetStatus(serverFolder).IsInstalled;

        public string GetConfigPath(string serverFolder)
            => Path.Join(serverFolder, ConfigFolder, UserConfigFileName);

        public ModStatus GetStatus(string serverFolder)
        {
            var status = new ModStatus { InstallPath = serverFolder };
            if (string.IsNullOrWhiteSpace(serverFolder)) return status;

            var pluginPath = GetInstalledPluginPath(serverFolder);
            if (pluginPath == null) return status;

            status.IsInstalled = true;
            status.Version = ReadFileVersion(pluginPath)
                // Fall back to the version reported in the player log after a server run
                ?? PlayerLogReader.Read().ValheimPlusVersion;

            return status;
        }

        public async Task<ModStatus> InstallAsync(string serverFolder, Action<string> onProgress = null)
        {
            EnsureBepInExInstalled(serverFolder);

            onProgress?.Invoke("Looking up the latest Valheim Plus release...");
            var release = await ModSourceClient.GetLatestValheimPlusAsync();

            var downloadUrl = release.GetWindowsServerDownloadUrl();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException("Unable to find a server download for the latest Valheim Plus release.");
            }

            var isDll = downloadUrl.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var extension = isDll ? ".dll" : ".zip";
            var tempFile = Path.Join(Path.GetTempPath(), "vsg-mods", $"ValheimPlus-{release.Version}{extension}");

            try
            {
                onProgress?.Invoke($"Downloading Valheim Plus {release.Version}...");
                await ModSourceClient.DownloadFileAsync(downloadUrl, tempFile);

                onProgress?.Invoke("Extracting Valheim Plus...");
                if (isDll)
                {
                    InstallPluginDll(serverFolder, tempFile);
                }
                else
                {
                    InstallFromZip(serverFolder, tempFile);
                }
            }
            finally
            {
                TryDeleteFile(tempFile);
            }

            Logger.Information("Installed Valheim Plus {version} into {folder}", release.Version, serverFolder);
            return GetStatus(serverFolder);
        }

        public Task<ModStatus> InstallFromFileAsync(string serverFolder, string zipPath, Action<string> onProgress = null)
        {
            EnsureBepInExInstalled(serverFolder);

            onProgress?.Invoke("Extracting Valheim Plus...");

            if (zipPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                InstallPluginDll(serverFolder, zipPath);
            }
            else
            {
                InstallFromZip(serverFolder, zipPath);
            }

            Logger.Information("Installed Valheim Plus from file {file} into {folder}", zipPath, serverFolder);
            return Task.FromResult(GetStatus(serverFolder));
        }

        public async Task<ModStatus> CheckForUpdateAsync(string serverFolder)
        {
            var status = GetStatus(serverFolder);

            var release = await ModSourceClient.GetLatestValheimPlusAsync();
            status.LatestVersion = release.Version;
            status.UpdateAvailable = ModVersion.IsNewer(release.Version, status.Version);

            return status;
        }

        private void EnsureBepInExInstalled(string serverFolder)
        {
            if (!IsBepInExInstalled(serverFolder))
            {
                throw new InvalidOperationException("BepInEx must be installed before Valheim Plus.");
            }
        }

        private void InstallFromZip(string serverFolder, string zipPath)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("Valheim Plus archive not found.", zipPath);

            Directory.CreateDirectory(Path.Join(serverFolder, PluginFolder));
            Directory.CreateDirectory(Path.Join(serverFolder, ConfigFolder));

            var stripPrefix = ZipHelper.DetectSingleRootPrefix(zipPath);

            // Prefer archives that already mirror the game folder layout (BepInEx/plugins/...)
            if (ContainsGameLayout(zipPath, prefix: string.Empty))
            {
                ZipHelper.ExtractZip(zipPath, serverFolder);
                return;
            }

            if (!string.IsNullOrEmpty(stripPrefix) && ContainsGameLayout(zipPath, stripPrefix))
            {
                ZipHelper.ExtractZip(zipPath, serverFolder, stripPrefix);
                return;
            }

            // Otherwise the archive holds the plugin dll (and maybe a default config) at its root
            ZipHelper.ExtractZip(zipPath, Path.Join(serverFolder, PluginFolder), stripPrefix);
            RouteBundledConfig(serverFolder);
        }

        private static bool ContainsGameLayout(string zipPath, string prefix)
            => Array.Exists(GameLayoutProbes, probe => ZipHelper.ContainsEntry(zipPath, prefix + probe));

        private static void RouteBundledConfig(string serverFolder)
        {
            var bundledConfig = Path.Join(serverFolder, PluginFolder, BundledConfigFileName);
            if (!File.Exists(bundledConfig)) return;

            var targetConfig = Path.Join(serverFolder, ConfigFolder, UserConfigFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetConfig));

            // Never overwrite an existing user configuration
            if (!File.Exists(targetConfig)) File.Move(bundledConfig, targetConfig);
            else File.Delete(bundledConfig);
        }

        private void InstallPluginDll(string serverFolder, string dllPath)
        {
            if (!File.Exists(dllPath)) throw new FileNotFoundException("Valheim Plus plugin not found.", dllPath);

            var targetPath = Path.Join(serverFolder, PluginFolder, "ValheimPlus.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.Copy(dllPath, targetPath, overwrite: true);
        }

        private static string GetInstalledPluginPath(string serverFolder)
        {
            foreach (var fileName in PluginFileNames)
            {
                var path = Path.Join(serverFolder, PluginFolder, fileName);
                if (File.Exists(path)) return path;
            }

            return null;
        }

        private static string ReadFileVersion(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                return !string.IsNullOrWhiteSpace(info.ProductVersion) ? info.ProductVersion : info.FileVersion;
            }
            catch
            {
                return null;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
