using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ValheimServerGUI.Tools.Data;

namespace ValheimServerGUI.Game
{
    public interface IPlayerDataRepository : IDataRepository<PlayerInfo>
    {
        event EventHandler<PlayerInfo> PlayerStatusChanged;

        IEnumerable<PlayerInfo> FindPlayersByQuery(PlayerDataQuery query);

        PlayerInfo SetPlayerJoining(string serverName, PlayerDataQuery query);

        PlayerInfo SetPlayerOnline(string serverName, string characterName, string zdoId);

        void SetPlayerLeaving(string serverName, PlayerDataQuery query);

        void SetPlayerOffline(string serverName, PlayerDataQuery query);

        Task LoadAsync();
    }
}