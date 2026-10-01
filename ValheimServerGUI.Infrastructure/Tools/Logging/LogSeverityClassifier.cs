using System;
using System.Text.RegularExpressions;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Logging.Components;

namespace ValheimServerGUI.Tools.Logging
{
    /// <summary>
    /// Works out how important a rendered log line is so the Logs tab can colour it. Pure and
    /// side-effect free; lives in Infrastructure because it reads both the Core log patterns and
    /// the Infrastructure level prefixes.
    /// </summary>
    public static class LogSeverityClassifier
    {
        /// <summary>
        /// Classifies a Valheim server stdout line. The line already carries the
        /// "[HH:mm:ss.fff] " prefix, which is why these are the same patterns the parser uses.
        /// </summary>
        public static LogSeverity ClassifyServerLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return LogSeverity.Normal;

            // Order matters. PlayerDied must be tested before PlayerConnected: the connected
            // pattern also matches the "0:0" line the game emits on death, which would make every
            // death look like a fresh login.
            if (IsMatch(line, ServerLogPatterns.PlayerDied)) return LogSeverity.Warning;

            if (IsMatch(line, ServerLogPatterns.PlayerDisconnectingWrongPassword) ||
                IsMatch(line, ServerLogPatterns.PlayerDisconnectingIncompatibleVersion))
            {
                return LogSeverity.Error;
            }

            if (IsMatch(line, ServerLogPatterns.ServerConnected) ||
                IsMatch(line, ServerLogPatterns.PlayerConnected) ||
                IsMatch(line, ServerLogPatterns.PlayerConnecting) ||
                IsMatch(line, ServerLogPatterns.PlayerConnectingCrossplay))
            {
                return LogSeverity.Success;
            }

            if (IsMatch(line, ServerLogPatterns.CrossplayJoinCodeActive) ||
                IsMatch(line, ServerLogPatterns.CrossplayJoinCodeRegistered) ||
                IsMatch(line, ServerLogPatterns.WorldSaved) ||
                IsMatch(line, ServerLogPatterns.WorldSavedLegacy))
            {
                return LogSeverity.Info;
            }

            return LogSeverity.Normal;
        }

        /// <summary>
        /// Classifies an application log line. The level survives only as the textual prefix added
        /// by <see cref="LogLevelTransformer"/>, so read it back from there.
        /// </summary>
        public static LogSeverity ClassifyApplicationLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return LogSeverity.Normal;

            // The line is "[HH:mm:ss.fff] [LVL] message", so look for the level after the timestamp.
            var timestampEnd = line.IndexOf(']');
            var body = timestampEnd >= 0 && timestampEnd + 1 < line.Length
                ? line[(timestampEnd + 1)..].TrimStart()
                : line;

            if (body.StartsWith(LogLevelTransformer.FatalPrefix, StringComparison.Ordinal) ||
                body.StartsWith(LogLevelTransformer.ErrorPrefix, StringComparison.Ordinal))
            {
                return LogSeverity.Error;
            }

            if (body.StartsWith(LogLevelTransformer.WarningPrefix, StringComparison.Ordinal))
            {
                return LogSeverity.Warning;
            }

            return LogSeverity.Normal;
        }

        private static bool IsMatch(string line, string pattern)
        {
            // ServerLogPatterns are literal constants, so they are always valid patterns.
            return Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase);
        }
    }
}