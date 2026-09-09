using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ValheimServerGUI.Tools;

namespace ValheimServerGUI.Game
{
    public static class ValheimPathExtensions
    {
        /// <summary>
        /// These are automatic backup files created by Valheim with the transition to
        /// the worlds_local folder on 6/20/22. Do not list these as world names.
        /// </summary>
        private static readonly Regex AutoBackupRegex = new(@"^.*?_backup_\d+?-\d+?");

        public static FileInfo GetValidatedServerExe(this IValheimServerOptions options)
        {
            return PathExtensions.GetFileInfo(options.ServerExePath, ".exe");
        }

        public static DirectoryInfo GetValidatedSaveDataFolder(this IValheimServerOptions options)
        {
            return PathExtensions.GetDirectoryInfo(options.SaveDataFolderPath, true);
        }

        public static List<string> GetWorldNames(this DirectoryInfo saveDataFolder)
        {
            try
            {
                var allNames = new List<string>();

                foreach (var info in saveDataFolder.GetWorldsFolders())
                {
                    if (!Directory.Exists(info.FullName)) continue;

                    // Legacy world format: a pair of files named <world>.fwl / <world>.db
                    allNames.AddRange(info
                        .GetFiles("*.fwl")
                        .Where(f => !AutoBackupRegex.IsMatch(f.Name))
                        .Select(f => Path.GetFileNameWithoutExtension(f.FullName)));

                    // New world format (1.0.x+): a folder named <world> containing
                    // a set of _main.<save number>.fwl2 / .db2 / .chunks files
                    allNames.AddRange(info
                        .GetDirectories()
                        .Where(d => !AutoBackupRegex.IsMatch(d.Name))
                        .Where(d => d.GetFiles("*.fwl2").Length > 0)
                        .Select(d => d.Name));
                }

                return allNames;
            }
            catch
            {
                // Return an empty list of names if we cannot load the worlds folders
                return new();
            }
        }

        public static bool IsWorldNameAvailable(this DirectoryInfo saveDataFolder, string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName)) return false;

            try
            {
                // Legacy format: <world>.fwl must not exist in any worlds folder
                var legacyExists = saveDataFolder.GetWorldsFolders()
                    .Select(p => Path.Join(p.FullName, $"{worldName}.fwl"))
                    .Any(p => File.Exists(p));

                if (legacyExists) return false;

                // New format: a folder named <world> w/ at least one .fwl2 file
                var newFormatExists = saveDataFolder.GetWorldsFolders()
                    .Select(p => Path.Join(p.FullName, worldName))
                    .Any(p => Directory.Exists(p) && Directory.GetFiles(p, "*.fwl2").Length > 0);

                return !newFormatExists;
            }
            catch
            {
                // Assume the world name is available if we cannot load the worlds folders
                return true;
            }
        }

        #region Helper methods

        private static IEnumerable<DirectoryInfo> GetWorldsFolders(this DirectoryInfo saveDataFolder)
        {
            yield return PathExtensions.GetDirectoryInfo(Path.Join(saveDataFolder.FullName, "worlds"));
            yield return PathExtensions.GetDirectoryInfo(Path.Join(saveDataFolder.FullName, "worlds_local"));
        }

        #endregion
    }
}
