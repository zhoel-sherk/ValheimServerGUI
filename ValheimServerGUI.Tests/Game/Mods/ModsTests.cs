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
        public async Task BepInExInstallPreservesExistingConfig()
        {
            var serverFolder = Path.Combine(TestFolder, "server-preserve");
            Directory.CreateDirectory(Path.Combine(serverFolder, "BepInEx", "config"));
            File.WriteAllText(Path.Combine(serverFolder, "BepInEx", "config", "BepInEx.cfg"), "user-edited");

            var zip = CreateZipFromEntries(
                ("BepInExPack_Valheim/winhttp.dll", "proxy"),
                ("BepInExPack_Valheim/BepInEx/core/BepInEx.dll", "core"),
                ("BepInExPack_Valheim/BepInEx/config/BepInEx.cfg", "default"));

            var manager = new BepInExManager(new Mock<IModSourceClient>().Object, Logger);
            await manager.InstallFromFileAsync(serverFolder, zip);

            Assert.Equal("user-edited", File.ReadAllText(Path.Combine(serverFolder, "BepInEx", "config", "BepInEx.cfg")));
        }

        [Fact]
        public async Task ValheimPlusInstallsGameLayoutArchive()
        {
            // BepInEx must be present first
            Directory.CreateDirectory(Path.Combine(TestFolder, "server-vp", "BepInEx", "core"));
            File.WriteAllText(Path.Combine(TestFolder, "server-vp", "BepInEx", "core", "BepInEx.dll"), "core");
            File.WriteAllText(Path.Combine(TestFolder, "server-vp", "winhttp.dll"), "proxy");

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
}
