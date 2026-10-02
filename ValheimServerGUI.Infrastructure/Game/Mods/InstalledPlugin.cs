using System;
using System.Diagnostics;
using System.IO;

namespace ValheimServerGUI.Game.Mods
{
    /// <summary>
    /// A mod assembly found in the BepInEx plugins folder. Read-only snapshot: this type
    /// never mutates the file it describes.
    /// </summary>
    public class InstalledPlugin
    {
        /// <summary>File name without extension, e.g. "ValheimPlus".</summary>
        public string Name { get; set; }

        /// <summary>File name including extension, e.g. "ValheimPlus.dll".</summary>
        public string FileName { get; set; }

        public string FullPath { get; set; }

        /// <summary>
        /// Assembly version from the file metadata. Empty when the DLL carries no version
        /// resource (common for BepInEx plugin stubs).
        /// </summary>
        public string Version { get; set; }

        public long SizeBytes { get; set; }

        public DateTime LastWriteTime { get; set; }

        /// <summary>
        /// True for the mods the app can install and update. Everything else is shown but
        /// is unmanaged - BepInEx has no on/off switch for a plugin, so removing it means
        /// moving the file out of the plugins folder.
        /// </summary>
        public bool IsManaged { get; set; }

        public string SizeDisplay
            => SizeBytes >= 1024 * 1024
                ? $"{SizeBytes / (1024.0 * 1024.0):0.#} MB"
                : $"{Math.Max(1, SizeBytes / 1024)} KB";

        public string ModifiedDisplay
            => LastWriteTime == default ? string.Empty : LastWriteTime.ToString("yyyy-MM-dd HH:mm");

        public static InstalledPlugin FromFile(string path, bool isManaged)
        {
            var info = new FileInfo(path);

            return new InstalledPlugin
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FileName = info.Name,
                FullPath = info.FullName,
                SizeBytes = info.Length,
                LastWriteTime = info.LastWriteTime,
                IsManaged = isManaged,
                Version = ReadVersion(path),
            };
        }

        /// <summary>
        /// Reads the assembly version. Any failure yields an empty string rather than
        /// throwing - a plugin without a version resource is normal, not an error.
        /// </summary>
        private static string ReadVersion(string path)
        {
            try
            {
                var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
                return string.IsNullOrWhiteSpace(version) ? string.Empty : version;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}