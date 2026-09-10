using System;

namespace ValheimServerGUI.Game.Mods
{
    public static class ModVersion
    {
        /// <summary>
        /// Returns true when the candidate version is newer than the baseline version.
        /// Falls back to a plain (case-insensitive) inequality check when either value
        /// is not parseable as a numeric version.
        /// </summary>
        public static bool IsNewer(string candidate, string baseline)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(baseline)) return false;

            if (Version.TryParse(candidate, out var candidateVersion) && Version.TryParse(baseline, out var baselineVersion))
            {
                return candidateVersion > baselineVersion;
            }

            return !string.Equals(candidate, baseline, StringComparison.OrdinalIgnoreCase);
        }
    }
}
