using System;
using System.IO;
using ValheimServerGUI.Game;
using Xunit;

namespace ValheimServerGUI.Core.Tests.Game
{
    public class ValheimPathExtensionsTests : IDisposable
    {
        private readonly string TestFolder = Path.Combine(Path.GetTempPath(), "vsg-core-tests", "worlds", Guid.NewGuid().ToString("N"));

        public ValheimPathExtensionsTests()
        {
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds_local"));
        }

        public void Dispose()
        {
            try { Directory.Delete(TestFolder, recursive: true); } catch { }
        }

        [Fact]
        public void DiscoversLegacyAndModernWorlds()
        {
            // Legacy layout: a pair of .fwl/.db files
            File.WriteAllText(Path.Combine(TestFolder, "worlds", "LegacyWorld.fwl"), "");
            File.WriteAllText(Path.Combine(TestFolder, "worlds", "LegacyWorld.db"), "");

            // Modern layout: a folder per world containing _main.*.fwl2 files
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds_local", "ModernWorld"));
            File.WriteAllText(Path.Combine(TestFolder, "worlds_local", "ModernWorld", "_main.1.fwl2"), "");
            File.WriteAllText(Path.Combine(TestFolder, "worlds_local", "ModernWorld", "_main.1.db2"), "");

            // Backups must be excluded
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds_local", "MyWorld_backup_auto-20260909-233240"));
            File.WriteAllText(Path.Combine(TestFolder, "worlds_local", "MyWorld_backup_auto-20260909-233240", "_main.1.fwl2"), "");

            var saveFolder = new DirectoryInfo(TestFolder);
            var names = saveFolder.GetWorldNames();

            Assert.Contains("LegacyWorld", names);
            Assert.Contains("ModernWorld", names);
            Assert.DoesNotContain(names, n => n.Contains("backup"));
        }

        [Fact]
        public void ReturnsEmptyListWhenFolderMissing()
        {
            var missing = new DirectoryInfo(Path.Combine(TestFolder, "does-not-exist"));
            Assert.Empty(missing.GetWorldNames());
        }

        [Fact]
        public void WorldNameUnavailableWhenModernWorldExists()
        {
            Directory.CreateDirectory(Path.Combine(TestFolder, "worlds_local", "TakenWorld"));
            File.WriteAllText(Path.Combine(TestFolder, "worlds_local", "TakenWorld", "_main.1.fwl2"), "");

            var saveFolder = new DirectoryInfo(TestFolder);
            Assert.False(saveFolder.IsWorldNameAvailable("TakenWorld"));
            Assert.True(saveFolder.IsWorldNameAvailable("FreeWorld"));
        }

        [Fact]
        public void WorldNameUnavailableWhenLegacyWorldExists()
        {
            File.WriteAllText(Path.Combine(TestFolder, "worlds", "TakenWorld.fwl"), "");

            var saveFolder = new DirectoryInfo(TestFolder);
            Assert.False(saveFolder.IsWorldNameAvailable("TakenWorld"));
        }

        [Fact]
        public void WorldNameAvailabilityHandlesBlankInput()
        {
            var saveFolder = new DirectoryInfo(TestFolder);
            Assert.False(saveFolder.IsWorldNameAvailable(null));
            Assert.False(saveFolder.IsWorldNameAvailable("  "));
        }
    }
}