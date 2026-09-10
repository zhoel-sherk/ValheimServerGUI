using System.Collections.Generic;

namespace ValheimServerGUI.Game
{
    /// <summary>
    /// A query describing how to find a player in the player data repository.
    /// Multiple criteria are AND'd together; the <see cref="Or"/> chain provides OR fallbacks.
    /// </summary>
    public class PlayerDataQuery
    {
        public string Platform;

        public string PlayerId;

        public string PlayerName;

        public string ZdoId;

        public string CharacterName;

        public PlayerDataQuery Or;

        public override string ToString()
        {
            var parameters = new List<string>();

            if (!string.IsNullOrWhiteSpace(Platform)) parameters.Add($"Platform={Platform}");
            if (!string.IsNullOrWhiteSpace(PlayerId)) parameters.Add($"PlayerId={PlayerId}");
            if (!string.IsNullOrWhiteSpace(PlayerName)) parameters.Add($"PlayerName={PlayerName}");
            if (!string.IsNullOrWhiteSpace(ZdoId)) parameters.Add($"ZdoId={ZdoId}");
            if (!string.IsNullOrWhiteSpace(CharacterName)) parameters.Add($"CharacterName={CharacterName}");

            var qs = string.Join("&", parameters);

            if (Or != null)
            {
                var qs2 = Or.ToString();
                if (!string.IsNullOrWhiteSpace(qs2))
                {
                    qs = $"{qs}|{qs2}";
                }
            }

            return qs;
        }

        public bool HasParameters() => !string.IsNullOrWhiteSpace(ToString());
    }
}