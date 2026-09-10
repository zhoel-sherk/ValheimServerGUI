using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Game.Mods
{
    public interface IBepInExManager
    {
        ModStatus GetStatus(string serverFolder);

        Task<ModStatus> InstallAsync(string serverFolder, Action<string> onProgress = null);

        Task<ModStatus> InstallFromFileAsync(string serverFolder, string zipPath, Action<string> onProgress = null);

        Task<ModStatus> CheckForUpdateAsync(string serverFolder);
    }

    /// <summary>
    /// Installs &amp; updates BepInEx for a Valheim dedicated server using the
    /// BepInExPack_Valheim distribution from Thunderstore.
    /// </summary>
    public class BepInExManager : IBepInExManager
    {
        private const string PackRootPrefix = "BepInExPack_Valheim/";
        private const string PackVersionMarkerFile = "BepInEx/.vsg-bepinex-pack-version";
        private const string DoorstopConfigFileName = "doorstop_config.ini";

        // Valheim 1.0.x runs Unity 6; BepInEx 5.4.23.3+ (pack 5.4.2333+) is the first
        // release that supports it. Older packs can crash the server on startup.
        private const string MinimumBepInExVersion = "5.4.23.3";

        private readonly IModSourceClient ModSourceClient;
        private readonly IApplicationLogger Logger;

        public BepInExManager(IModSourceClient modSourceClient, IApplicationLogger logger)
        {
            ModSourceClient = modSourceClient;
            Logger = logger;
        }

        public ModStatus GetStatus(string serverFolder)
        {
            var status = new ModStatus { InstallPath = serverFolder };
            if (string.IsNullOrWhiteSpace(serverFolder)) return status;

            var winhttpPath = Path.Join(serverFolder, "winhttp.dll");
            var doorstopPath = Path.Join(serverFolder, DoorstopConfigFileName);
            var corePath = Path.Join(serverFolder, "BepInEx", "core", "BepInEx.dll");
            if (!File.Exists(winhttpPath) || !File.Exists(doorstopPath) || !File.Exists(corePath)) return status;

            status.IsInstalled = true;
            status.Version = ReadFileVersion(corePath);
            status.PackageVersion = ReadInstalledPackVersion(serverFolder);

            return status;
        }

        public async Task<ModStatus> InstallAsync(string serverFolder, Action<string> onProgress = null)
        {
            onProgress?.Invoke("Looking up the latest BepInExPack...");
            var release = await ModSourceClient.GetLatestBepInExPackAsync();

            var tempZip = Path.Join(Path.GetTempPath(), "vsg-mods", $"BepInExPack_Valheim-{release.Version}.zip");
            try
            {
                onProgress?.Invoke($"Downloading BepInExPack {release.Version}...");
                await ModSourceClient.DownloadFileAsync(release.DownloadUrl, tempZip);

                onProgress?.Invoke("Extracting BepInEx...");
                InstallFromZip(serverFolder, tempZip, release.Version);
            }
            finally
            {
                TryDeleteFile(tempZip);
            }

            Logger.Information("Installed BepInExPack {version} into {folder}", release.Version, serverFolder);
            return GetStatus(serverFolder);
        }

        public Task<ModStatus> InstallFromFileAsync(string serverFolder, string zipPath, Action<string> onProgress = null)
        {
            onProgress?.Invoke("Extracting BepInEx...");
            InstallFromZip(serverFolder, zipPath, packVersion: null);

            WarnIfPackPredatesUnity6(serverFolder);

            Logger.Information("Installed BepInEx from file {zip} into {folder}", zipPath, serverFolder);
            return Task.FromResult(GetStatus(serverFolder));
        }

        public async Task<ModStatus> CheckForUpdateAsync(string serverFolder)
        {
            var status = GetStatus(serverFolder);

            var release = await ModSourceClient.GetLatestBepInExPackAsync();
            status.LatestVersion = release.Version;
            status.UpdateAvailable = ModVersion.IsNewer(release.Version, status.PackageVersion);

            return status;
        }

        private void InstallFromZip(string serverFolder, string zipPath, string packVersion)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("BepInEx archive not found.", zipPath);

            // Thunderstore packages wrap their contents in a root folder; plain BepInEx
            // release archives do not. Strip the root folder when one is present.
            var stripPrefix = ZipHelper.DetectSingleRootPrefix(zipPath) ?? PackRootPrefix;

            // Preserve any existing BepInEx configuration files
            bool ShouldOverwrite(string relativePath)
                => !relativePath.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);

            var extracted = ZipHelper.ExtractZip(zipPath, serverFolder, stripPrefix, ShouldOverwrite);
            if (extracted == 0) throw new InvalidOperationException("The archive did not contain any extractable files.");

            // BepInEx expects a plugins folder, but it may not exist in the archive
            Directory.CreateDirectory(Path.Join(serverFolder, "BepInEx", "plugins"));
            Directory.CreateDirectory(Path.Join(serverFolder, "BepInEx", "config"));

            if (!string.IsNullOrWhiteSpace(packVersion))
            {
                WriteInstalledPackVersion(serverFolder, packVersion);
            }

            // BepInEx enables its console by default, which can take over the stdout pipe
            // the GUI relies on for log parsing. Force it off after every install/update.
            ApplyServerLoggingDefaults(serverFolder);
        }

        /// <summary>
        /// Warns when a manually installed BepInEx predates Unity 6 support. Best-effort;
        /// version detection only works for real BepInEx assemblies.
        /// </summary>
        private void WarnIfPackPredatesUnity6(string serverFolder)
        {
            try
            {
                var version = ReadFileVersion(Path.Join(serverFolder, "BepInEx", "core", "BepInEx.dll"));
                if (string.IsNullOrWhiteSpace(version)) return;

                if (ModVersion.IsNewer(MinimumBepInExVersion, version))
                {
                    Logger.Warning(
                        "The installed BepInEx version {version} predates {minimum} and may not work with Valheim 1.0.x (Unity 6). Consider installing the latest BepInExPack_Valheim.",
                        version,
                        MinimumBepInExVersion);
                }
            }
            catch
            {
                // Version detection is best-effort
            }
        }

        /// <summary>
        /// Disables the BepInEx console in BepInEx/config/BepInEx.cfg so that server stdout
        /// is not diverted away from the GUI's redirected pipe. Preserves all other settings.
        /// </summary>
        private static void ApplyServerLoggingDefaults(string serverFolder)
        {
            try
            {
                var configPath = Path.Join(serverFolder, "BepInEx", "config", "BepInEx.cfg");
                if (!File.Exists(configPath)) return;

                var updated = BepInExConfig.DisableConsoleLogging(File.ReadAllText(configPath));
                File.WriteAllText(configPath, updated);
            }
            catch
            {
                // Best-effort: a broken config should never abort the install
            }
        }

        private static string ReadInstalledPackVersion(string serverFolder)
        {
            try
            {
                var markerPath = Path.Join(serverFolder, PackVersionMarkerFile);
                if (File.Exists(markerPath))
                {
                    var version = File.ReadAllText(markerPath).Trim();
                    if (!string.IsNullOrWhiteSpace(version)) return version;
                }
            }
            catch
            {
                // Fall through to the player log below
            }

            // Fall back to the version reported in the player log (available after a server run)
            return PlayerLogReader.Read().BepInExPackVersion;
        }

        private static void WriteInstalledPackVersion(string serverFolder, string packVersion)
        {
            try
            {
                var markerPath = Path.Join(serverFolder, PackVersionMarkerFile);
                File.WriteAllText(markerPath, packVersion);
            }
            catch
            {
                // The marker is only used for update checks; ignore failures
            }
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
