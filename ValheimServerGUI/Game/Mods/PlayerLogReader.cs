using System;
using System.IO;
using System.Text.RegularExpressions;
using ValheimServerGUI.Properties;

namespace ValheimServerGUI.Game.Mods
{
    /// <summary>
    /// Mod version information scraped from the Unity/BepInEx player log.
    /// BepInEx &amp; Valheim Plus both write their versions (and update state) to this log,
    /// making it the most reliable source of truth once the server has been started.
    /// </summary>
    public class PlayerLogModInfo
    {
        public string BepInExVersion { get; set; }

        public string BepInExPackVersion { get; set; }

        public string ValheimPlusVersion { get; set; }

        /// <summary>
        /// The raw update status reported by Valheim Plus itself, e.g. "is up to date".
        /// </summary>
        public string ValheimPlusUpdateStatus { get; set; }
    }

    public static class PlayerLogReader
    {
        // [Message:   BepInEx] BepInEx 5.4.23.5 - valheim (3/28/2023 9:32:11 PM)
        private static readonly Regex BepInExVersionRegex = new(@"BepInEx\s+([\d\.]+)\s+-\s+valheim", RegexOptions.IgnoreCase);

        // [Message:   BepInEx] User is running BepInExPack Valheim version 5.4.2350 from Thunderstore
        private static readonly Regex BepInExPackVersionRegex = new(@"BepInExPack Valheim version\s+([\d\.]+)", RegexOptions.IgnoreCase);

        // [Info   :   BepInEx] Loading [Valheim Plus 0.10.0.2]
        private static readonly Regex ValheimPlusVersionRegex = new(@"Loading \[Valheim Plus\s+([\d\.]+)\]", RegexOptions.IgnoreCase);

        // [Info   :Valheim Plus] ValheimPlus [0.10.0.2] is up to date.
        private static readonly Regex ValheimPlusUpdateRegex = new(@"ValheimPlus \[[\d\.]+\]\s+([^\r\n\]]+)", RegexOptions.IgnoreCase);

        public static string GetPlayerLogPath()
            => Environment.ExpandEnvironmentVariables(Resources.PlayerLogFilePath);

        /// <summary>
        /// Reads mod information from the player log. Returns an empty result when the log
        /// does not exist or cannot be read.
        /// </summary>
        public static PlayerLogModInfo Read(string playerLogPath = null)
        {
            try
            {
                playerLogPath ??= GetPlayerLogPath();
                if (!File.Exists(playerLogPath)) return new PlayerLogModInfo();

                return Parse(File.ReadAllText(playerLogPath));
            }
            catch
            {
                return new PlayerLogModInfo();
            }
        }

        public static PlayerLogModInfo Parse(string logText)
        {
            var info = new PlayerLogModInfo();
            if (string.IsNullOrWhiteSpace(logText)) return info;

            var bepInExMatch = BepInExVersionRegex.Match(logText);
            if (bepInExMatch.Success) info.BepInExVersion = bepInExMatch.Groups[1].Value;

            var packMatch = BepInExPackVersionRegex.Match(logText);
            if (packMatch.Success) info.BepInExPackVersion = packMatch.Groups[1].Value;

            var vPlusMatch = ValheimPlusVersionRegex.Match(logText);
            if (vPlusMatch.Success) info.ValheimPlusVersion = vPlusMatch.Groups[1].Value;

            var vPlusUpdateMatch = ValheimPlusUpdateRegex.Match(logText);
            if (vPlusUpdateMatch.Success) info.ValheimPlusUpdateStatus = vPlusUpdateMatch.Groups[1].Value.Trim();

            return info;
        }
    }
}
