using System;
using System.Collections.Generic;
using System.IO;
using ValheimServerGUI.Game;
using Xunit;

namespace ValheimServerGUI.Core.Tests.Game
{
    public class ValheimServerOptionsTests : IDisposable
    {
        private static readonly string TestFolder = Path.Combine(Path.GetTempPath(), "vsg-core-tests", Guid.NewGuid().ToString("N"));

        public ValheimServerOptionsTests()
        {
            Directory.CreateDirectory(TestFolder);
        }

        public void Dispose()
        {
            try { Directory.Delete(TestFolder, recursive: true); } catch { }
        }

        private ValheimServerOptions CreateValidOptions()
        {
            var exePath = Path.Combine(TestFolder, "valheim_server.exe");
            File.WriteAllText(exePath, string.Empty);

            return new ValheimServerOptions
            {
                Name = "Test Server",
                Password = "hunter2",
                WorldName = "Test World",
                Public = false,
                Port = 2456,
                SaveInterval = 30,
                Backups = 1,
                BackupShort = 60,
                BackupLong = 120,
                ServerExePath = exePath,
                SaveDataFolderPath = TestFolder,
            };
        }

        [Fact]
        public void ValidOptionsPassValidation()
        {
            var options = CreateValidOptions();
            options.Validate();
        }

        [Fact]
        public void ServerNameIsRequired()
        {
            var options = CreateValidOptions();
            options.Name = null;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void WorldNameIsRequired()
        {
            var options = CreateValidOptions();
            options.WorldName = null;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void ServerNameCannotMatchWorldName()
        {
            var options = CreateValidOptions();
            options.WorldName = options.Name;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void PublicServerRequiresPassword()
        {
            var options = CreateValidOptions();
            options.PasswordValidation = true;
            options.Public = true;
            options.Password = null;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void PasswordMustBeAtLeastFiveCharacters()
        {
            var options = CreateValidOptions();
            options.PasswordValidation = true;
            options.Password = "hunt";
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(65536)]
        public void PortMustBeInRange(int port)
        {
            var options = CreateValidOptions();
            options.Port = port;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void SaveIntervalMustBePositive()
        {
            var options = CreateValidOptions();
            options.SaveInterval = 0;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void BackupIntervalsMustBeOrdered()
        {
            var options = CreateValidOptions();
            options.BackupShort = options.BackupLong + 1;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void RejectsLogFileArgument()
        {
            var options = CreateValidOptions();
            options.AdditionalArgs = "-logFile server.log";
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void RejectsInvalidPreset()
        {
            var options = CreateValidOptions();
            options.WorldPreset = "bogus";
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void PresetOverridesModifiers()
        {
            var options = CreateValidOptions();
            options.WorldPreset = WorldGenPresets.Hard;
            options.WorldModifiers = new Dictionary<string, string> { { WorldGenModifiers.Raids, WorldGenModifiers.Values.RaidsNone } };
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void RejectsInvalidModifierValue()
        {
            var options = CreateValidOptions();
            options.WorldModifiers = new Dictionary<string, string> { { WorldGenModifiers.Raids, "bogus" } };
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void AcceptsValidModifier()
        {
            var options = CreateValidOptions();
            options.WorldModifiers = new Dictionary<string, string> { { WorldGenModifiers.Raids, WorldGenModifiers.Values.RaidsNone } };
            options.Validate();
        }

        [Fact]
        public void RejectsInvalidWorldKey()
        {
            var options = CreateValidOptions();
            options.WorldKeys = new HashSet<string> { "bogus" };
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void AcceptsValidWorldKey()
        {
            var options = CreateValidOptions();
            options.WorldKeys = new HashSet<string> { WorldGenKeys.NoMap };
            options.Validate();
        }

        [Fact]
        public void RejectsMissingServerExe()
        {
            var options = CreateValidOptions();
            options.ServerExePath = Path.Combine(TestFolder, "does-not-exist.exe");
            Assert.Throws<FileNotFoundException>(() => options.Validate());
        }

        [Fact]
        public void RejectsMissingSaveDataFolder()
        {
            var options = CreateValidOptions();
            options.SaveDataFolderPath = Path.Combine(TestFolder, "missing");
            Assert.Throws<DirectoryNotFoundException>(() => options.Validate());
        }
    }
}