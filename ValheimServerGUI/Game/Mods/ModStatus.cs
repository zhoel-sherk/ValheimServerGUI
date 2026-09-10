namespace ValheimServerGUI.Game.Mods
{
    /// <summary>
    /// The installation state & version information for an installable server mod.
    /// </summary>
    public class ModStatus
    {
        public bool IsInstalled { get; set; }

        /// <summary>
        /// The version of the installed mod (e.g. from the mod DLL or core assembly).
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The version of the distribution package, when it differs from the mod version.
        /// For BepInEx, this is the BepInExPack_Valheim version.
        /// </summary>
        public string PackageVersion { get; set; }

        /// <summary>
        /// The latest version available from the upstream source, when known.
        /// </summary>
        public string LatestVersion { get; set; }

        /// <summary>
        /// True when the installed version is older than the known latest version.
        /// </summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// The folder that the mod is installed into (the Valheim server directory).
        /// </summary>
        public string InstallPath { get; set; }
    }
}
