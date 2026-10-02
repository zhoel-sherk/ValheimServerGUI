using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Tools.Logging;
using Xunit;

namespace ValheimServerGUI.Tests.Game.Mods
{
    public class ModVersionTests
    {
        [Theory]
        [InlineData("0.10.0.2", "0.10.0.1", true)]
        [InlineData("0.10.0.1", "0.10.0.2", false)]
        [InlineData("0.10.0.2", "0.10.0.2", false)]
        [InlineData("5.4.2350", "5.4.2202", true)]
        [InlineData(null, "1.0.0", false)]
        [InlineData("1.0.0", null, false)]
        public void IsNewerWorks(string candidate, string baseline, bool expected)
        {
            Assert.Equal(expected, ModVersion.IsNewer(candidate, baseline));
        }
    }

    public class BepInExConfigTests
    {
        [Theory]
        [InlineData("[Logging.Console]\nEnabled = true\nStandardOutType = Auto", "[Logging.Console]\nEnabled = false\nStandardOutType = Auto")]
        [InlineData("[Logging.Console]\r\nEnabled = true\r\n", "[Logging.Console]\r\nEnabled = false\r\n")]
        [InlineData("[General]\nSomething = 1\n\n[Logging.Console]\n# Setting type: Boolean\nEnabled = true\n", "[General]\nSomething = 1\n\n[Logging.Console]\n# Setting type: Boolean\nEnabled = false\n")]
        public void DisablesConsoleLogging(string input, string expected)
        {
            Assert.Equal(expected, BepInExConfig.DisableConsoleLogging(input));
        }

        [Fact]
        public void AppendsConsoleSectionWhenMissing()
        {
            var input = "[General]\nSomething = 1\n";

            var result = BepInExConfig.DisableConsoleLogging(input);

            Assert.Contains("[Logging.Console]", result);
            Assert.Contains("Enabled = false", result);
            Assert.Contains("Something = 1", result);
        }

        [Fact]
        public void PreservesAlreadyDisabledConsole()
        {
            var input = "[Logging.Console]\nEnabled = false\nOther = true\n";

            Assert.Equal(input, BepInExConfig.DisableConsoleLogging(input));
        }
    }

    public class PlayerLogReaderTests
    {
        private const string SampleLog = @"
[Message:   BepInEx] BepInEx 5.4.23.5 - valheim (3/28/2023 9:32:11 PM)
[Message:   BepInEx] User is running BepInExPack Valheim version 5.4.2350 from Thunderstore
[Info   :   BepInEx] Loading [Valheim Plus 0.10.0.2]
[Info   :Valheim Plus] ValheimPlus [0.10.0.2] is up to date.
[Info   : Unity Log] 03/28/2023 23:14:38: Valheim version:1.0.7 (network version 39)
";

        [Fact]
        public void ParsesAllVersions()
        {
            var info = PlayerLogReader.Parse(SampleLog);

            Assert.Equal("5.4.23.5", info.BepInExVersion);
            Assert.Equal("5.4.2350", info.BepInExPackVersion);
            Assert.Equal("0.10.0.2", info.ValheimPlusVersion);
            Assert.Equal("is up to date.", info.ValheimPlusUpdateStatus);
        }

        [Fact]
        public void EmptyInputReturnsEmptyInfo()
        {
            var info = PlayerLogReader.Parse(null);

            Assert.Null(info.BepInExVersion);
            Assert.Null(info.ValheimPlusVersion);
        }
    }

    public class ZipHelperTests : IDisposable
    {
        private readonly string TestFolder = Path.Combine(Path.GetTempPath(), "vsg-tests", "zip", Guid.NewGuid().ToString("N"));

        public ZipHelperTests() => Directory.CreateDirectory(TestFolder);

        public void Dispose()
        {
            try { Directory.Delete(TestFolder, recursive: true); } catch { }
        }

        private string CreateZip(params string[] entries)
        {
            var zipPath = Path.Combine(TestFolder, "archive.zip");

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var entry in entries)
            {
                var archiveEntry = zip.CreateEntry(entry);
                using var writer = new StreamWriter(archiveEntry.Open());
                writer.Write("test");
            }

            return zipPath;
        }

        [Fact]
        public void DetectsSingleRootPrefix()
        {
            var zip = CreateZip("Wrapper/a.txt", "Wrapper/BepInEx/b.dll");

            Assert.Equal("Wrapper/", ZipHelper.DetectSingleRootPrefix(zip));
        }

        [Fact]
        public void NoRootPrefixWhenMultipleRoots()
        {
            var zip = CreateZip("a.txt", "b/BepInEx/b.dll");

            Assert.Null(ZipHelper.DetectSingleRootPrefix(zip));
        }

        [Fact]
        public void ExtractsWithStrippedRoot()
        {
            var zip = CreateZip("Wrapper/a.txt", "Wrapper/BepInEx/plugins/b.dll");
            var destination = Path.Combine(TestFolder, "out");

            var count = ZipHelper.ExtractZip(zip, destination, "Wrapper/");

            Assert.Equal(2, count);
            Assert.True(File.Exists(Path.Combine(destination, "a.txt")));
            Assert.True(File.Exists(Path.Combine(destination, "BepInEx", "plugins", "b.dll")));
        }

        [Fact]
        public void OverwritePredicatePreservesExistingFiles()
        {
            var zip = CreateZip("config/keep.cfg", "core/replace.dll");
            var destination = Path.Combine(TestFolder, "out2");
            Directory.CreateDirectory(Path.Combine(destination, "config"));
            File.WriteAllText(Path.Combine(destination, "config", "keep.cfg"), "original");

            ZipHelper.ExtractZip(zip, destination, null, path => !path.StartsWith("config/"));

            Assert.Equal("original", File.ReadAllText(Path.Combine(destination, "config", "keep.cfg")));
            Assert.True(File.Exists(Path.Combine(destination, "core", "replace.dll")));
        }

        [Fact]
        public void ContainsEntryIsCaseInsensitive()
        {
            var zip = CreateZip("BepInEx/plugins/ValheimPlus.dll");

            Assert.True(ZipHelper.ContainsEntry(zip, "bepinex/PLUGINS/valheimplus.dll"));
            Assert.False(ZipHelper.ContainsEntry(zip, "BepInEx/plugins/Other.dll"));
        }
    }

    public class BackupServiceTests : IDisposable
    {
        private readonly string TestFolder = Path.Combine(Path.GetTempPath(), "vsg-tests", "backups", Guid.NewGuid().ToString("N"));

        public BackupServiceTests()
        {
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds_local"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds"));
        }

        public void Dispose()
        {
            try { Directory.Delete(TestFolder, recursive: true); } catch { }
        }

        [Fact]
        public void FindsModernAndLegacyBackups()
        {
            // Modern directory-based backup
            var modernBackup = Path.Combine(TestFolder, "worlds_local", "MyWorld_backup_auto-20260909-233240");
            Directory.CreateDirectory(modernBackup);
            File.WriteAllText(Path.Combine(modernBackup, "_main.10.db2"), "data");

            // Legacy file-based backup (a pair of db/fwl files)
            File.WriteAllText(Path.Combine(TestFolder, "worlds", "MyWorld_backup_20220912-142618.db"), "legacy");
            File.WriteAllText(Path.Combine(TestFolder, "worlds", "MyWorld_backup_20220912-142618.fwl"), "meta");

            // Live worlds (must not be treated as backups)
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds_local", "MyWorld"));
            File.WriteAllText(Path.Combine(TestFolder, "worlds_local", "MyWorld", "_main.1.db2"), "live");

            var service = new BackupService();
            var backups = service.GetBackups(new DirectoryInfo(TestFolder));

            Assert.Equal(2, backups.Count);
            Assert.Equal("MyWorld", backups[0].WorldName);
            Assert.True(backups[0].IsDirectory);
            Assert.Equal(new DateTime(2026, 9, 9, 23, 32, 40), backups[0].Timestamp);
            Assert.Equal(new DateTime(2022, 9, 12, 14, 26, 18), backups[1].Timestamp);
        }

        [Fact]
        public void StatusReportsHealthyWhenRecent()
        {
            var recent = Path.Combine(TestFolder, "worlds_local", $"MyWorld_backup_auto-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(recent);
            File.WriteAllText(Path.Combine(recent, "_main.1.db2"), "data");

            var service = new BackupService();
            var status = service.GetStatus(new DirectoryInfo(TestFolder), expectedCount: 4, shortIntervalSeconds: 7200);

            Assert.True(status.HasBackups);
            Assert.True(status.IsHealthy);
            Assert.NotNull(status.Latest);
        }

        [Fact]
        public void StatusReportsUnhealthyWhenEmpty()
        {
            var service = new BackupService();
            var status = service.GetStatus(new DirectoryInfo(TestFolder), expectedCount: 4, shortIntervalSeconds: 7200);

            Assert.False(status.HasBackups);
            Assert.False(status.IsHealthy);
        }
    }

    public class ValheimPlusReleaseTests
    {
        private static ValheimPlusRelease ReleaseWith(params string[] assetNames) => new()
        {
            Version = "0.10.0.2",
            Assets = assetNames.Select(n => new ValheimPlusAsset { Name = n, DownloadUrl = $"https://example.com/{n}" }).ToArray(),
        };

        [Fact]
        public void PrefersPlainWindowsServerAsset()
        {
            var release = ReleaseWith("ValheimPlus.dll", "WindowsServerRenamed.zip", "WindowsServer.zip");

            Assert.EndsWith("WindowsServer.zip", release.GetWindowsServerDownloadUrl());
        }

        [Fact]
        public void AvoidsRenamedAssetWhenPlainMissing()
        {
            var release = ReleaseWith("ValheimPlus.dll", "WindowsServerRenamed.zip");

            Assert.EndsWith("ValheimPlus.dll", release.GetWindowsServerDownloadUrl());
        }
    }

    public class ModInstallTests : IDisposable
    {
        private readonly string TestFolder = Path.Combine(Path.GetTempPath(), "vsg-tests", "install", Guid.NewGuid().ToString("N"));

        private readonly IApplicationLogger Logger = new Mock<IApplicationLogger>().Object;

        public ModInstallTests() => Directory.CreateDirectory(TestFolder);

        public void Dispose()
        {
            try { Directory.Delete(TestFolder, recursive: true); } catch { }
        }

        private string CreateZipFromEntries(params (string Path, string Content)[] entries)
        {
            var zipPath = Path.Combine(TestFolder, $"{Guid.NewGuid():N}.zip");

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var (path, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write(content);
            }

            return zipPath;
        }

        [Fact]
        public async Task BepInExInstallsFromPackStyleArchive()
        {
            var zip = CreateZipFromEntries(
                ("BepInExPack_Valheim/winhttp.dll", "proxy"),
                ("BepInExPack_Valheim/doorstop_config.ini", "config"),
                ("BepInExPack_Valheim/BepInEx/core/BepInEx.dll", "core"),
                ("BepInExPack_Valheim/BepInEx/config/BepInEx.cfg", "cfg"));

            var manager = new BepInExManager(new Mock<IModSourceClient>().Object, Logger);
            var serverFolder = Path.Combine(TestFolder, "server");

            var status = await manager.InstallFromFileAsync(serverFolder, zip);

            Assert.True(status.IsInstalled);
            Assert.True(File.Exists(Path.Combine(serverFolder, "winhttp.dll")));
            Assert.True(File.Exists(Path.Combine(serverFolder, "BepInEx", "core", "BepInEx.dll")));
            Assert.True(Directory.Exists(Path.Combine(serverFolder, "BepInEx", "plugins")));
        }

        [Fact]
        public async Task BepInExInstallPreservesExistingConfigAndDisablesConsole()
        {
            var serverFolder = Path.Combine(TestFolder, "server-preserve");
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "config"));
            File.WriteAllText(
                Path.Combine(serverFolder, "BepInEx", "config", "BepInEx.cfg"),
                "[General]\nEnableConsole = true\n\n[Logging.Console]\nEnabled = true\nLogLevels = Fatal,Error");

            var zip = CreateZipFromEntries(
                ("BepInExPack_Valheim/winhttp.dll", "proxy"),
                ("BepInExPack_Valheim/doorstop_config.ini", "config"),
                ("BepInExPack_Valheim/BepInEx/core/BepInEx.dll", "core"),
                ("BepInExPack_Valheim/BepInEx/config/BepInEx.cfg", "default"));

            var manager = new BepInExManager(new Mock<IModSourceClient>().Object, Logger);
            await manager.InstallFromFileAsync(serverFolder, zip);

            var config = File.ReadAllText(Path.Combine(serverFolder, "BepInEx", "config", "BepInEx.cfg"));
            Assert.Contains("EnableConsole = true", config);
            Assert.Contains("LogLevels = Fatal,Error", config);
            Assert.Contains("Enabled = false", config);
        }

        [Fact]
        public void GetStatusRequiresDoorstopConfig()
        {
            var manager = new BepInExManager(new Mock<IModSourceClient>().Object, Logger);

            var serverFolder = Path.Combine(TestFolder, "status-doorstop");
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "core"));
            File.WriteAllText(Path.Combine(serverFolder, "winhttp.dll"), "proxy");
            File.WriteAllText(Path.Combine(serverFolder, "BepInEx", "core", "BepInEx.dll"), "core");

            Assert.False(manager.GetStatus(serverFolder).IsInstalled);

            File.WriteAllText(Path.Combine(serverFolder, "doorstop_config.ini"), "config");
            Assert.True(manager.GetStatus(serverFolder).IsInstalled);
        }

        [Fact]
        public async Task ValheimPlusInstallsGameLayoutArchive()
        {
            // BepInEx must be present first
            Directory.CreateDirectory(Path.Combine(TestFolder, "server-vp", "BepInEx", "core"));
            File.WriteAllText(Path.Combine(TestFolder, "server-vp", "BepInEx", "core", "BepInEx.dll"), "core");
            File.WriteAllText(Path.Combine(TestFolder, "server-vp", "winhttp.dll"), "proxy");
            File.WriteAllText(Path.Combine(TestFolder, "server-vp", "doorstop_config.ini"), "config");

            var zip = CreateZipFromEntries(("BepInEx/plugins/ValheimPlus.dll", "plugin"));

            var bepInEx = new BepInExManager(new Mock<IModSourceClient>().Object, Logger);
            var manager = new ValheimPlusManager(new Mock<IModSourceClient>().Object, bepInEx, Logger);

            var status = await manager.InstallFromFileAsync(Path.Combine(TestFolder, "server-vp"), zip);

            Assert.True(status.IsInstalled);
            Assert.True(File.Exists(Path.Combine(TestFolder, "server-vp", "BepInEx", "plugins", "ValheimPlus.dll")));
        }

        [Fact]
        public async Task ValheimPlusInstallsRootDllArchive()
        {
            var serverFolder = Path.Combine(TestFolder, "server-vp2");
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "core"));
            File.WriteAllText(Path.Combine(serverFolder, "BepInEx", "core", "BepInEx.dll"), "core");
            File.WriteAllText(Path.Combine(serverFolder, "winhttp.dll"), "proxy");
            File.WriteAllText(Path.Combine(serverFolder, "doorstop_config.ini"), "config");

            var zip = CreateZipFromEntries(
                ("ValheimPlus.dll", "plugin"),
                ("valheim_plus.cfg", "default-config"));

            var bepInEx = new BepInExManager(new Mock<IModSourceClient>().Object, Logger);
            var manager = new ValheimPlusManager(new Mock<IModSourceClient>().Object, bepInEx, Logger);

            var status = await manager.InstallFromFileAsync(serverFolder, zip);

            Assert.True(status.IsInstalled);
            Assert.True(File.Exists(Path.Combine(serverFolder, "BepInEx", "plugins", "ValheimPlus.dll")));
            Assert.True(File.Exists(manager.GetConfigPath(serverFolder)));
            Assert.False(File.Exists(Path.Combine(serverFolder, "BepInEx", "plugins", "valheim_plus.cfg")));
        }

        [Fact]
        public async Task ValheimPlusRequiresBepInEx()
        {
            var zip = CreateZipFromEntries(("BepInEx/plugins/ValheimPlus.dll", "plugin"));
            var serverFolder = Path.Combine(TestFolder, "no-bepinex");

            var bepInEx = new BepInExManager(new Mock<IModSourceClient>().Object, Logger);
            var manager = new ValheimPlusManager(new Mock<IModSourceClient>().Object, bepInEx, Logger);

            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallFromFileAsync(serverFolder, zip));
        }
    }

    /// <summary>
    /// BepInExLogReader replaces the client-log fallback: a dedicated host has no
    /// %LOCALAPPDATA%\IronGate\Valheim\Player.log, which silently made the pack version null and
    /// therefore made "check for updates" always report nothing to do.
    /// </summary>
    public class BepInExLogReaderTests
    {
        // Real lines from a dedicated server's BepInEx/LogOutput.log.
        private const string ServerLog = """
            [Message:   BepInEx] BepInEx 5.4.23.5 - valheim_server (9/9/2026 9:07:58 PM)
            [Message:   BepInEx] User is running BepInExPack Valheim version 5.4.2350 from Thunderstore
            [Info   : BepInEx] Chainloader started
            [Info   : Unity Log] Console: ValheimPlus [0.10.1.1] is loaded.
            [Info   : Unity Log] Console: ValheimPlus [0.10.1.1] is outdated, version [0.10.2.0] is available.
            """;

        [Fact]
        public void ParsesAllVersionsFromServerLog()
        {
            var info = BepInExLogReader.Parse(ServerLog);

            Assert.Equal("5.4.23.5", info.BepInExVersion);
            Assert.Equal("5.4.2350", info.BepInExPackVersion);
            Assert.Equal("0.10.1.1", info.ValheimPlusVersion);
            Assert.Contains("outdated", info.ValheimPlusUpdateStatus);
            Assert.Contains("0.10.2.0", info.ValheimPlusUpdateStatus);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyLogYieldsNoVersions(string logText)
        {
            var info = BepInExLogReader.Parse(logText);

            Assert.Null(info.BepInExPackVersion);
            Assert.Null(info.ValheimPlusVersion);
        }

        [Fact]
        public void LogWithoutPackLineYieldsNoPackVersion()
        {
            // e.g. a BepInEx installed by hand that never got far enough to print the pack line
            var info = BepInExLogReader.Parse("[Info : BepInEx] Chainloader started");

            Assert.Null(info.BepInExPackVersion);
        }

        [Fact]
        public void ReadReturnsEmptyInfoForMissingFolder()
        {
            var info = BepInExLogReader.Read(Path.Combine(Path.GetTempPath(), "vsg-no-such-server-" + Guid.NewGuid().ToString("N")));

            Assert.Null(info.BepInExPackVersion);
        }

        [Fact]
        public void ReadPicksUpTheServerLogOnDisk()
        {
            var folder = Path.Combine(Path.GetTempPath(), "vsg-tests", "bepilog", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "BepInEx"));
            File.WriteAllText(Path.Combine(folder, "BepInEx", "LogOutput.log"), ServerLog);

            try
            {
                Assert.Equal("5.4.2350", BepInExLogReader.Read(folder).BepInExPackVersion);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    public class BepInExManagerRepairTests : IDisposable
    {
        private readonly string TestFolder =
            Path.Combine(Path.GetTempPath(), "vsg-tests", "repair", Guid.NewGuid().ToString("N"));

        private readonly IApplicationLogger Logger = new Mock<IApplicationLogger>().Object;

        private IBepInExManager Manager => new BepInExManager(new Mock<IModSourceClient>().Object, Logger);

        public BepInExManagerRepairTests() => Directory.CreateDirectory(TestFolder);

        public void Dispose()
        {
            try { Directory.Delete(TestFolder, recursive: true); } catch { }
        }

        private string WriteConfig(string content, string folderName = "server")
        {
            var serverFolder = Path.Combine(TestFolder, folderName);
            var configFolder = Path.Combine(serverFolder, "BepInEx", "config");
            Directory.CreateDirectory(configFolder);

            var path = Path.Combine(configFolder, "BepInEx.cfg");
            File.WriteAllText(path, content);
            return serverFolder;
        }

        [Fact]
        public void DisablesConsoleLoggingAndReportsTheChange()
        {
            var serverFolder = WriteConfig("[Logging.Console]\nEnabled = true\nPreventClose = true\n");

            Assert.True(Manager.EnsureConsoleLoggingDisabled(serverFolder));

            var updated = File.ReadAllText(Path.Combine(serverFolder, "BepInEx", "config", "BepInEx.cfg"));
            Assert.Contains("Enabled = false", updated);
            Assert.Contains("PreventClose = true", updated);
        }

        [Fact]
        public void IsANoOpWhenAlreadyDisabled()
        {
            const string content = "[Logging.Console]\nEnabled = false\n";
            var serverFolder = WriteConfig(content);

            Assert.False(Manager.EnsureConsoleLoggingDisabled(serverFolder));

            // Byte-identical: a no-op must not rewrite the file at all.
            Assert.Equal(content, File.ReadAllText(Path.Combine(serverFolder, "BepInEx", "config", "BepInEx.cfg")));
        }

        [Fact]
        public void IsANoOpWhenThereIsNoBepInExConfig()
        {
            var serverFolder = Path.Combine(TestFolder, "empty");
            Directory.CreateDirectory(serverFolder);

            Assert.False(Manager.EnsureConsoleLoggingDisabled(serverFolder));
        }

        [Fact]
        public void IsANoOpForABlankFolder()
        {
            Assert.False(Manager.EnsureConsoleLoggingDisabled(null));
            Assert.False(Manager.EnsureConsoleLoggingDisabled("   "));
        }

        [Fact]
        public void ListsInstalledPluginsAndSkipsSidecarFiles()
        {
            var serverFolder = Path.Combine(TestFolder, "plugins-server");
            var plugins = Path.Combine(serverFolder, "BepInEx", "plugins");
            Directory.CreateDirectory(plugins);

            File.WriteAllText(Path.Combine(plugins, "ValheimPlus.dll"), "x");
            File.WriteAllText(Path.Combine(plugins, "Jotunn.dll"), "yy");
            File.WriteAllText(Path.Combine(plugins, "Jotunn.pdb"), "zzz");
            File.WriteAllText(Path.Combine(plugins, "Jotunn.xml"), "docs");

            var pluginsList = Manager.GetInstalledPlugins(serverFolder);

            Assert.Equal(2, pluginsList.Count);
            Assert.Equal("Jotunn", pluginsList[0].Name);
            Assert.Equal("ValheimPlus", pluginsList[1].Name);

            // Only the mod the app can install/update is marked managed.
            Assert.False(pluginsList[0].IsManaged);
            Assert.True(pluginsList[1].IsManaged);
        }

        [Fact]
        public void PluginListingIsEmptyForAFolderWithoutBepInEx()
        {
            var serverFolder = Path.Combine(TestFolder, "no-bepinex");
            Directory.CreateDirectory(serverFolder);

            Assert.Empty(Manager.GetInstalledPlugins(serverFolder));
            Assert.Empty(Manager.GetInstalledPlugins(null));
        }

        [Fact]
        public void PackVersionComesFromTheServerLogWhenTheMarkerIsMissing()
        {
            var serverFolder = Path.Combine(TestFolder, "log-only");
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "core"));
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "config"));
            File.WriteAllText(Path.Combine(serverFolder, "winhttp.dll"), "x");
            File.WriteAllText(Path.Combine(serverFolder, "doorstop_config.ini"), "x");
            File.WriteAllText(Path.Combine(serverFolder, "BepInEx", "core", "BepInEx.dll"), "x");
            File.WriteAllText(
                Path.Combine(serverFolder, "BepInEx", "LogOutput.log"),
                "[Message:   BepInEx] User is running BepInExPack Valheim version 5.4.2350 from Thunderstore");

            var status = Manager.GetStatus(serverFolder);

            Assert.True(status.IsInstalled);
            Assert.Equal("5.4.2350", status.PackageVersion);
        }

        [Fact]
        public void MarkerFileWinsOverTheLog()
        {
            var serverFolder = Path.Combine(TestFolder, "marker-wins");
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "core"));
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "config"));
            File.WriteAllText(Path.Combine(serverFolder, "winhttp.dll"), "x");
            File.WriteAllText(Path.Combine(serverFolder, "doorstop_config.ini"), "x");
            File.WriteAllText(Path.Combine(serverFolder, "BepInEx", "core", "BepInEx.dll"), "x");
            File.WriteAllText(Path.Combine(serverFolder, "BepInEx", ".vsg-bepinex-pack-version"), "5.4.2400");
            File.WriteAllText(
                Path.Combine(serverFolder, "BepInEx", "LogOutput.log"),
                "[Message:   BepInEx] User is running BepInExPack Valheim version 5.4.2350 from Thunderstore");

            Assert.Equal("5.4.2400", Manager.GetStatus(serverFolder).PackageVersion);
        }
    }
}
