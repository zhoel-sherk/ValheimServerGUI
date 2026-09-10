using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Player row in the Players list.
    /// </summary>
    public partial class PlayerRowViewModel : ObservableObject
    {
        private readonly PlayerInfo Player;

        public string DisplayName => GetPlayerDisplayName(Player);
        public string Platform => Player.Platform;
        public string PlayerId => Player.PlayerId;
        public string Status => Player.PlayerStatus.ToString();
        public string LastUpdated => new TimeAgo(Player.LastStatusChange).ToString();

        public PlayerRowViewModel(PlayerInfo player)
        {
            Player = player;
        }

        private static string GetPlayerDisplayName(PlayerInfo player)
        {
            var name = player.PlayerName ?? $"[...{player.PlayerId[^4..]}]";

            if (!string.IsNullOrWhiteSpace(player.LastStatusCharacter))
            {
                name += $" ({player.LastStatusCharacter})";
            }

            return name;
        }
    }

    /// <summary>
    /// Players tab (Phase 2 step 5): lists cached players, reflects live status changes and
    /// provides remove actions.
    /// </summary>
    public partial class PlayersViewModel : ObservableObject
    {
        private readonly IPlayerDataRepository PlayerDataProvider;
        private readonly IApplicationLogger Logger;

        public ObservableCollection<PlayerRowViewModel> Players { get; } = new();

        [ObservableProperty]
        private PlayerRowViewModel? _selectedPlayer;

        public PlayersViewModel(IPlayerDataRepository playerDataProvider, IApplicationLogger logger)
        {
            PlayerDataProvider = playerDataProvider;
            Logger = logger;

            PlayerDataProvider.DataReady += OnDataReady;
            PlayerDataProvider.EntityUpdated += OnEntityUpdated;
            PlayerDataProvider.PlayerStatusChanged += OnPlayerStatusChanged;

            LoadPlayers();
        }

        private void LoadPlayers()
        {
            foreach (var player in PlayerDataProvider.Data.OrderBy(p => p.PlayerName))
            {
                Players.Add(new PlayerRowViewModel(player));
            }
        }

        private void OnDataReady(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(LoadPlayers);
        }

        private void OnEntityUpdated(object? sender, PlayerInfo player)
        {
            Dispatcher.UIThread.Post(() => UpsertRow(player));
        }

        private void OnPlayerStatusChanged(object? sender, PlayerInfo player)
        {
            Dispatcher.UIThread.Post(() => UpsertRow(player));
        }

        private void UpsertRow(PlayerInfo player)
        {
            var existing = Players.FirstOrDefault(p => p.Platform == player.Platform && p.PlayerId == player.PlayerId);
            if (existing != null)
            {
                var index = Players.IndexOf(existing);
                Players[index] = new PlayerRowViewModel(player);
            }
            else
            {
                Players.Add(new PlayerRowViewModel(player));
            }
        }

        [RelayCommand]
        private void RemovePlayer()
        {
            if (SelectedPlayer == null) return;

            var platform = SelectedPlayer.Platform;
            var playerId = SelectedPlayer.PlayerId;
            var displayName = SelectedPlayer.DisplayName;

            var player = PlayerDataProvider.Data.FirstOrDefault(p => p.Platform == platform && p.PlayerId == playerId);
            if (player == null)
            {
                Logger.Warning("Unable to remove player: no player found on platform {platform}", platform);
                return;
            }

            PlayerDataProvider.Remove(player.Key);
            Players.Remove(SelectedPlayer);
            Logger.Information("Removed player: {playerId}", playerId);
        }

        [RelayCommand]
        private void Refresh()
        {
            Players.Clear();
            LoadPlayers();
        }
    }
}