using System;

namespace ValheimServerGUI.Infrastructure
{
    /// <summary>
    /// Platform-neutral application settings previously sourced from the WinForms
    /// <c>Resources.resx</c>. Kept here so Infrastructure stays free of
    /// <c>System.Drawing</c>/WinForms while preserving the exact default values.
    /// </summary>
    public static class AppSettings
    {
        // Paths (used via Environment.ExpandEnvironmentVariables by callers)
        public const string DefaultGamePath = "%ProgramFiles(x86)%\\Steam\\steamapps\\common\\Valheim\\valheim.exe";
        public const string DefaultServerPath = "%ProgramFiles(x86)%\\Steam\\steamapps\\common\\Valheim dedicated server\\valheim_server.exe";
        public const string DefaultValheimSaveFolder = "%USERPROFILE%\\AppData\\LocalLow\\IronGate\\Valheim";
        public const string LogsFolderPath = "%USERPROFILE%\\AppData\\LocalLow\\Runeberry\\ValheimServerGUI\\logs";
        public const string PlayerListFilePath = "%USERPROFILE%\\AppData\\LocalLow\\Runeberry\\ValheimServerGUI\\players-cache.json";
        public const string PlayerLogFilePath = "%USERPROFILE%\\AppData\\LocalLow\\IronGate\\Valheim\\Player.log";
        public const string UserPrefsFilePath = "%USERPROFILE%\\AppData\\LocalLow\\Runeberry\\ValheimServerGUI\\userprefs.txt";
        public const string UserPrefsFilePathV2 = "%USERPROFILE%\\AppData\\LocalLow\\Runeberry\\ValheimServerGUI\\userprefs.json";

        // Defaults
        public const string DefaultBackupCount = "4";
        public const string DefaultBackupIntervalLong = "43200";
        public const string DefaultBackupIntervalShort = "7200";
        public const string DefaultSaveInterval = "1800";
        public const string DefaultServerPort = "2456";
        public const string DefaultServerProfileName = "Default";
        public static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromDays(1);

        // URLs
        public const string UrlBepInExPack = "https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/";
        public const string UrlBepInExPackApi = "https://thunderstore.io/api/experimental/package/denikson/BepInExPack_Valheim/";
        public const string UrlValheimPlusApi = "https://api.github.com/repos/Grantapher/ValheimPlus/releases/latest";
        public const string UrlValheimPlus = "https://github.com/Grantapher/ValheimPlus";
        public const string UrlDotnetDownload = "https://dotnet.microsoft.com/download/dotnet/10.0";
        public const string UrlExternalIpLookup = "https://api.ipify.org?format=json";
        public const string UrlGithubApi = "https://api.github.com/repos/runeberry/ValheimServerGUI";
        public const string UrlRuneberryApi = "https://u312zw22d6.execute-api.us-east-1.amazonaws.com/Prod";
    }
}