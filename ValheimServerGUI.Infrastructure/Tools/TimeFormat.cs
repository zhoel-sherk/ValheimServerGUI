using System.Globalization;

namespace ValheimServerGUI.Tools
{
    /// <summary>
    /// Renders a duration in seconds the way a person would say it.
    /// </summary>
    /// <remarks>
    /// The server takes its save and backup intervals in raw seconds, so the UI has to offer
    /// both: the exact number to edit and a readable hint like "≈ 30 min" next to it. Fixing the
    /// choices to a dropdown was rejected - it would make values like 45 or 90 minutes
    /// unreachable. See TODO.md.
    ///
    /// Units are deliberately kept short and non-localised ("min", "h") rather than run through
    /// Humanizer: these sit under a numeric field the user is editing, and matching the English
    /// unit abbreviations used across the app reads more cleanly there.
    /// </remarks>
    public static class TimeFormat
    {
        private const int SecondsPerMinute = 60;
        private const int SecondsPerHour = 3600;

        /// <summary>
        /// Formats a duration in seconds, e.g. 1800 -> "30 min", 43200 -> "12 h",
        /// 5400 -> "1 h 30 min". Non-positive or meaningless input yields an empty string.
        /// </summary>
        public static string FormatDuration(int seconds)
        {
            if (seconds <= 0) return string.Empty;

            if (seconds < SecondsPerHour)
            {
                return seconds % SecondsPerMinute == 0
                    ? Pluralize(seconds / SecondsPerMinute, "min")
                    : string.Format(CultureInfo.InvariantCulture, "{0} s", seconds);
            }

            var hours = seconds / SecondsPerHour;
            var minutes = (seconds % SecondsPerHour) / SecondsPerMinute;

            var text = Pluralize(hours, "h");
            return minutes == 0
                ? text
                : string.Format(CultureInfo.InvariantCulture, "{0} {1} min", text, minutes);
        }

        private static string Pluralize(int value, string unit)
            => value == 1 ? $"1 {unit}" : $"{value} {unit}";
    }
}