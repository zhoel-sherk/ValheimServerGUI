using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// Player details dialog: view a player's status and edit their name / character list
    /// (parity with the WinForms PlayerDetailsForm).
    /// </summary>
    public partial class PlayerDetailsViewModel : ObservableObject
    {
        private const string NotAvailable = "N/A";

        private readonly IPlayerDataRepository PlayerDataProvider;
        private readonly IUserInteraction UserInteraction;
        private readonly IApplicationLogger Logger;

        private string? PlayerKey;

        public ObservableCollection<string> Characters { get; } = new();

        [ObservableProperty] private string? _playerName;
        [ObservableProperty] private string? _platform;
        [ObservableProperty] private string? _playerId;
        [ObservableProperty] private string? _zdoId;
        [ObservableProperty] private string? _currentCharacter;
        [ObservableProperty] private string? _status;
        [ObservableProperty] private string? _lastUpdated;
        [ObservableProperty] private string? _selectedCharacter;

        public PlayerDetailsViewModel(
            IPlayerDataRepository playerDataProvider,
            IUserInteraction userInteraction,
            IApplicationLogger logger)
        {
            PlayerDataProvider = playerDataProvider;
            UserInteraction = userInteraction;
            Logger = logger;
        }

        public void Load(string playerKey)
        {
            PlayerKey = playerKey;
            Refresh();
        }

        private PlayerInfo? FindPlayer()
        {
            return string.IsNullOrWhiteSpace(PlayerKey) ? null : PlayerDataProvider.FindById(PlayerKey);
        }

        [RelayCommand]
        private void Refresh()
        {
            var player = FindPlayer();
            if (player == null)
            {
                PlayerName = null;
                return;
            }

            PlayerName = player.PlayerName;
            Platform = player.Platform;
            PlayerId = player.PlayerId;
            ZdoId = string.IsNullOrWhiteSpace(player.ZdoId) ? NotAvailable : player.ZdoId;
            CurrentCharacter = string.IsNullOrWhiteSpace(player.LastStatusCharacter) ? NotAvailable : player.LastStatusCharacter;
            Status = player.PlayerStatus.ToString();
            LastUpdated = new TimeAgo(player.LastStatusChange).ToString();

            Characters.Clear();
            foreach (var character in player.Characters ?? new List<PlayerInfo.CharacterInfo>())
            {
                Characters.Add(character.CharacterName);
            }
        }

        [RelayCommand]
        private async Task AddCharacterAsync()
        {
            var name = await PromptForCharacterAsync("Add Character", "Add a Valheim character name for this player:", null);
            if (name == null || Characters.Contains(name)) return;

            Characters.Add(name);
        }

        [RelayCommand]
        private async Task EditCharacterAsync()
        {
            if (SelectedCharacter == null) return;

            var name = await PromptForCharacterAsync("Edit Character", $"Edit the name for character '{SelectedCharacter}'", SelectedCharacter);
            if (name == null) return;

            var index = Characters.IndexOf(SelectedCharacter);
            if (index >= 0) Characters[index] = name;
        }

        [RelayCommand]
        private void RemoveCharacter()
        {
            if (SelectedCharacter != null) Characters.Remove(SelectedCharacter);
        }

        [RelayCommand]
        private void Save()
        {
            var player = FindPlayer();
            if (player == null) return;

            var newCharacters = new List<PlayerInfo.CharacterInfo>();
            foreach (var characterName in Characters)
            {
                var existing = player.Characters?.FirstOrDefault(c => c.CharacterName == characterName);
                newCharacters.Add(existing ?? new PlayerInfo.CharacterInfo { CharacterName = characterName, MatchConfident = true });
            }

            player.PlayerName = PlayerName;
            player.Characters = newCharacters;

            PlayerDataProvider.Upsert(player);
            Logger.Information("Saved player details for {key}", PlayerKey);
        }

        private Task<string?> PromptForCharacterAsync(string title, string message, string? initialValue)
        {
            return UserInteraction.PromptForTextAsync(
                title,
                message,
                initialValue,
                input => string.IsNullOrWhiteSpace(input) || input.Length > 64
                    ? "Name must be between 1-64 characters."
                    : null);
        }
    }
}
