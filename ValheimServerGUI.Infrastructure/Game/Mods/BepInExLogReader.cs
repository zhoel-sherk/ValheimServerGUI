using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ValheimServerGUI.Game.Mods
{
    /// <summary>Versions scraped from a BepInEx log file.</summary>
    public class BepInExLogModInfo
    {
        /// <summary>BepInEx core version, e.g. "5.4.23.5".</summary>
        public string BepInExVersion { get; set; }

        /// <summary>BepInExPack_Valheim package version, e.g. "5.4.2350".</summary>
        public string BepInExPackVersion { get; set; }

        /// <summary>Valheim Plus version, e.g. "0.10.1.1".</summary>
        public string ValheimPlusVersion { get; set; }

        /// <summary>
        /// Valheim Plus's own update message, e.g. "is outdated, version [0.10.2.0] is available."
        /// </summary>
        public string ValheimPlusUpdateStatus { get; set; }
    }

    /// <summary>
    /// Reads mod/BepInEx versions out of the dedicated server's own
    /// <c>BepInEx/LogOutput.log</c>.
    ///
    /// This is the authoritative source on a headless host. <see cref="PlayerLogReader"/> reads the
    /// Valheim *client* log under %LOCALAPPDATA%\IronGate\Valheim, which a dedicated machine may
    /// not have at all - and when it was missing, the pack version came back null, so
    /// <see cref="ModVersion.IsNewer"/> returned false and "check for updates" silently reported
    /// nothing to do.
    ///
    /// BepInEx is configured with <c>AppendLog = false</c>, so LogOutput.log holds only the most
    /// recent session; LogOutput.log.1 (the rotated older one) is deliberately not read.
    /// </summary>
    public static class BepInExLogReader
    {
        // "BepInEx 5.4.23.5 - valheim_server (9/9/2026 9:07:58 PM)"
        private static readonly Regex BepInExVersionRegex =
            new(@"BepInEx (\d[\d\.]+)\s+-\s+", RegexOptions.IgnoreCase);

        // "User is running BepInExPack Valheim version 5.4.2350 from Thunderstore"
        private static readonly Regex PackVersionRegex =
            new(@"BepInExPack Valheim version\s+([\d\.]+)", RegexOptions.IgnoreCase);

        // "ValheimPlus [0.10.1.1] is loaded." / "ValheimPlus [0.10.1.1] is outdated, ..."
        private static readonly Regex ValheimPlusVersionRegex =
            new(@"ValheimPlus \[([\d\.]+)\]", RegexOptions.IgnoreCase);

        // "ValheimPlus [0.10.1.1] is loaded."  /  "... is outdated, version [0.10.2.0] is available."
        // Only the update phrasings count. A generic capture would latch onto the first
        // "is loaded." line the log contains and report that as the update status.
        private static readonly Regex ValheimPlusUpdateRegex = new(
            @"ValheimPlus \[[\d\.]+\]\s+(is outdated, version \[[\d\.]+\] is available\.|is up to date\.)",
            RegexOptions.IgnoreCase);

        /// <summary>Full path of the server's BepInEx log.</summary>
        public static string GetLogPath(string serverFolder)
            => string.IsNullOrWhiteSpace(serverFolder) ? null : Path.Join(serverFolder, "BepInEx", "LogOutput.log");

        /// <summary>
        /// Reads versions from the server log. Returns empty information when the file is
        /// missing or unreadable - never throws.
        /// </summary>
        public static BepInExLogModInfo Read(string serverFolder)
        {
            try
            {
                var path = GetLogPath(serverFolder);
                if (path == null || !File.Exists(path)) return new BepInExLogModInfo();

                return Parse(File.ReadAllText(path));
            }
            catch
            {
                return new BepInExLogModInfo();
            }
        }

        /// <summary>Testable parser: extracts versions from log text.</summary>
        public static BepInExLogModInfo Parse(string logText)
        {
            var info = new BepInExLogModInfo();
            if (string.IsNullOrWhiteSpace(logText)) return info;

            var bepInExMatch = BepInExVersionRegex.Match(logText);
            if (bepInExMatch.Success) info.BepInExVersion = bepInExMatch.Groups[1].Value;

            var packMatch = PackVersionRegex.Match(logText);
            if (packMatch.Success) info.BepInExPackVersion = packMatch.Groups[1].Value;

            var vPlusMatch = ValheimPlusVersionRegex.Match(logText);
            if (vPlusMatch.Success) info.ValheimPlusVersion = vPlusMatch.Groups[1].Value;

            var vPlusUpdateMatch = ValheimPlusUpdateRegex.Match(logText);
            if (vPlusUpdateMatch.Success) info.ValheimPlusUpdateStatus = vPlusUpdateMatch.Groups[1].Value.Trim();

            return info;
        }
    }
}