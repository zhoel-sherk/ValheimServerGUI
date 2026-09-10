using System;

namespace ValheimServerGUI.Game.Mods
{
    /// <summary>
    /// Minimal, lossless edits to a BepInEx config file (BepInEx/config/BepInEx.cfg).
    /// </summary>
    public static class BepInExConfig
    {
        /// <summary>
        /// Disables BepInEx's console output (<c>[Logging.Console] Enabled = false</c>) while
        /// preserving every other setting and comment. The console must stay off so that the
        /// redirected stdout the GUI parses is not diverted to a separate window.
        /// Appends the section when the file does not already contain it.
        /// </summary>
        public static string DisableConsoleLogging(string configText)
        {
            if (string.IsNullOrWhiteSpace(configText)) return configText;

            var eol = configText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = configText.Replace("\r\n", "\n").Split('\n');

            var inConsoleSection = false;
            var changed = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    inConsoleSection = string.Equals(trimmed, "[Logging.Console]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inConsoleSection && trimmed.StartsWith("Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = "Enabled = false";
                    changed = true;
                    inConsoleSection = false;
                    break;
                }
            }

            if (!changed)
            {
                if (!configText.EndsWith(eol, StringComparison.Ordinal)) configText += eol;
                return configText + "[Logging.Console]" + eol + "Enabled = false" + eol;
            }

            return string.Join(eol, lines);
        }
    }
}