using BeatSaberMarkupLanguage;
using System;
using System.Linq;
using TMPro;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistantShared.Models;
using UnityEngine;
using Zenject;

namespace TournamentAssistant.Behaviors
{
    internal class FloatingScoreScreen : IInitializable, IDisposable
    {
        private readonly GameObject _canvas;
        private readonly TextMeshProUGUI _scoreboardText;
        private readonly RoomCoordinator _roomCoordinator;

        public FloatingScoreScreen(RoomCoordinator roomCoordinator)
        {
            _roomCoordinator = roomCoordinator;
            _canvas = new GameObject("FloatingScoreScreen");
            _canvas.transform.position = new Vector3(0, 9f, 10f);
            _canvas.transform.eulerAngles = new Vector3(0, 0, 0);
            _canvas.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            _canvas.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _scoreboardText = BeatSaberUI.CreateText((_canvas.transform as RectTransform)!, "1: Moon - 15182\n2: Moon - 15182\n3: Moon - 15182\n4: Moon - 15182\n5: Moon - 15182", new Vector2(.5f, 0));
            _scoreboardText.fontSize = 12f;
            _scoreboardText.lineSpacing = -40f;
        }

        public void Initialize()
        {
            Plugin.client.PlayerInfoUpdated += Client_PlayerInfoUpdated;
        }

        public void Dispose()
        {
            Plugin.client.PlayerInfoUpdated -= Client_PlayerInfoUpdated;
        }

        private void Client_PlayerInfoUpdated(Player player)
        {
            if (_roomCoordinator.Match is null)
                return;

            if (_roomCoordinator.Match.Players.Contains(player))
            {
                _roomCoordinator.Match.Players.First(x => x == player).Score = player.Score;
                var leaderboard = _roomCoordinator.Match.Players.OrderByDescending(x => x.Score).Take(5);

                var index = 1;
                var leaderboardText = string.Empty;
                foreach (var leaderboardPlayer in leaderboard) leaderboardText += $"{index++}: {leaderboardPlayer.Name} - {leaderboardPlayer.Score}\n";
                UnityMainThreadDispatcher.Instance().Enqueue(() => _scoreboardText.SetText(leaderboardText));
            }
        }
    }
}