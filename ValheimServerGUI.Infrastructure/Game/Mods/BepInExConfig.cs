using System;
using System.Collections.Generic;
using System.Text;

namespace ValheimServerGUI.Game.Mods
{
    /// <summary>
    /// Minimal, lossless edits to a BepInEx config file (BepInEx/config/BepInEx.cfg).
    /// </summary>
    public static class BepInExConfig
    {
        private const string ConsoleSection = "[Logging.Console]";
        private const string DisabledValue = "false";

        /// <summary>
        /// True when the config explicitly disables BepInEx's console, or when the file says
        /// nothing about the console at all (nothing to fix). False only when the console is
        /// actually enabled.
        /// </summary>
        public static bool IsConsoleLoggingDisabled(string configText)
        {
            var index = FindConsoleEnabledLine(configText, out var line);
            if (index < 0) return true;   // no section or no key: nothing enabled

            return !IsTrueValue(GetEnabledValue(line));
        }

        /// <summary>
        /// Disables BepInEx's console output (<c>[Logging.Console] Enabled = false</c>) while
        /// preserving every other setting, comment and the original line endings. The console must
        /// stay off so the redirected stdout the GUI parses is not diverted to a separate window.
        /// Appends the section when the file does not already contain it.
        ///
        /// Returns the input unchanged when the console is already disabled, so calling this on
        /// every server start never rewrites the user's file.
        /// </summary>
        public static string DisableConsoleLogging(string configText)
        {
            if (string.IsNullOrWhiteSpace(configText)) return configText;

            var index = FindConsoleEnabledLine(configText, out var line);
            if (index < 0)
            {
                return AppendConsoleSection(configText);
            }

            if (IsConsoleLoggingDisabled(configText)) return configText;

            var lines = SplitKeepingEndings(configText);
            var terminator = GetTerminator(lines[index], configText);
            lines[index] = "Enabled = " + DisabledValue + terminator;

            return string.Concat(lines);
        }

        private static string AppendConsoleSection(string configText)
        {
            var eol = configText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            if (!configText.EndsWith(eol, StringComparison.Ordinal)) configText += eol;

            return configText + ConsoleSection + eol + "Enabled = " + DisabledValue + eol;
        }

        /// <summary>
        /// Locates the line that sets the console's Enabled key. Returns the index into the
        /// newline-split form of the text, or -1 when there is none.
        /// </summary>
        private static int FindConsoleEnabledLine(string configText, out string line)
        {
            line = null;
            if (string.IsNullOrEmpty(configText)) return -1;

            var lines = SplitKeepingEndings(configText);
            var inConsoleSection = false;

            for (var i = 0; i < lines.Count; i++)
            {
                var trimmed = GetContent(lines[i]).Trim();

                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    inConsoleSection = string.Equals(trimmed, ConsoleSection, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inConsoleSection && trimmed.StartsWith("Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    line = lines[i];
                    return i;
                }
            }

            return -1;
        }

        private static string GetEnabledValue(string line)
        {
            var content = GetContent(line);
            var equals = content.IndexOf('=');
            if (equals < 0) return string.Empty;

            return content[(equals + 1)..].Trim();
        }

        private static bool IsTrueValue(string value)
            => value.Equals("true", StringComparison.OrdinalIgnoreCase);

        private static string GetContent(string line)
        {
            var end = line.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? line : line[..end];
        }

        private static string GetTerminator(string line, string fallbackSource)
        {
            if (line.EndsWith("\r\n", StringComparison.Ordinal)) return "\r\n";
            if (line.EndsWith("\n", StringComparison.Ordinal)) return "\n";
            if (line.EndsWith("\r", StringComparison.Ordinal)) return "\r";

            return fallbackSource.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        }

        /// <summary>
        /// Splits on newlines but keeps each line's terminator, so a rewrite can leave every
        /// untouched byte exactly as it was. Mixed-ending files stay mixed.
        /// </summary>
        private static List<string> SplitKeepingEndings(string text)
        {
            var result = new List<string>();
            var start = 0;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;

                result.Add(text[start..(i + 1)]);
                start = i + 1;
            }

            if (start < text.Length) result.Add(text[start..]);

            return result;
        }
    }
}