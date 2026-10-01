using System.Collections.Generic;

namespace ValheimServerGUI.Tools.Models
{
    public static class PlayerPlatforms
    {
        public const string Steam = "Steam";
        public const string Xbox = "Xbox";
        public const string PlayStation = "PlayStation";
        public const string Nintendo = "Nintendo";

        /// <summary>
        /// Platforms we know by name. Unknown platforms are still accepted (see
        /// <see cref="TryGetValidPlatform"/>) - this set only drives display hints and tests.
        /// </summary>
        public static readonly HashSet<string> All = new()
        {
            Steam,
            Xbox,
            PlayStation,
            Nintendo,
        };

        /// <summary>
        /// Resolves a platform name from a crossplay log capture such as "Steam" or "Switch".
        /// The accepted tokens are the ones the game prints in its crossplay lines; known ones map
        /// to our canonical name (the game's "Switch" token becomes Nintendo). Anything else
        /// non-blank is kept as-is rather than rejected, since dropping it would silently hide
        /// the player from the list.
        /// </summary>
        public static bool TryGetValidPlatform(string input, out string platform)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                platform = null;
                return false;
            }

            switch (input.Trim().ToLowerInvariant())
            {
                case "steam":
                    platform = Steam;
                    return true;
                case "xbox":
                    platform = Xbox;
                    return true;
                case "playstation":
                    platform = PlayStation;
                    return true;
                case "nintendo":
                case "switch":
                    platform = Nintendo;
                    return true;
                default:
                    platform = NormaliseUnknown(input.Trim());
                    return true;
            }
        }

        private static string NormaliseUnknown(string value)
        {
            // Leave an already-nice name alone; otherwise upper-case the first character so
            // "epic" is displayed as "Epic".
            if (char.IsUpper(value[0])) return value;

            return char.ToUpperInvariant(value[0]) + value[1..];
        }
    }
}