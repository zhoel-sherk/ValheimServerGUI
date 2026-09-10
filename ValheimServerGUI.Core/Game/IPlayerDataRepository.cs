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

        PlayerInfo SetPlayerJoining(PlayerDataQuery query);

        PlayerInfo SetPlayerOnline(string characterName, string zdoId);

        void SetPlayerLeaving(PlayerDataQuery query);

        void SetPlayerOffline(PlayerDataQuery query);

        Task LoadAsync();
    }
}