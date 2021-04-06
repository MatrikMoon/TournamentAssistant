using BeatSaberMarkupLanguage;
using System.Linq;
using TMPro;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistantShared.Models;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class FloatingScoreScreen : MonoBehaviour
    {
        public static FloatingScoreScreen Instance { get; set; }

        private Player[] players;
        private TextMeshProUGUI _scoreboardText;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);

            gameObject.transform.position = new Vector3(0, 9f, 10f);
            gameObject.transform.eulerAngles = new Vector3(0, 0, 0);
            gameObject.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

            var mainCanvas = gameObject.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.WorldSpace;

            _scoreboardText = BeatSaberUI.CreateText(transform as RectTransform, "1: Moon - 15182\n2: Moon - 15182\n3: Moon - 15182\n4: Moon - 15182\n5: Moon - 15182", new Vector2(.5f, 0));
            _scoreboardText.fontSize = 12f;
            _scoreboardText.lineSpacing = -40f;

            //Figure out what players we're meant to be collecting scores for
            var match = Resources.FindObjectsOfTypeAll<RoomCoordinator>().FirstOrDefault()?.Match;
            players = match.Players.ToArray();

            Plugin.client.PlayerInfoUpdated += Client_PlayerInfoUpdated;
        }

        private void Client_PlayerInfoUpdated(Player player)
        {
            if (players.Contains(player))
            {
                players.First(x => x == player).Score = player.Score;
                var leaderboard = players.OrderByDescending(x => x.Score).Take(5);

                var index = 1;
                var leaderboardText = string.Empty;
                foreach (var leaderboardPlayer in leaderboard) leaderboardText += $"{index++}: {leaderboardPlayer.Name} - {leaderboardPlayer.Score}\n";

                UnityMainThreadDispatcher.Instance().Enqueue(() => _scoreboardText.SetText(leaderboardText));
            }
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Plugin.client.PlayerInfoUpdated -= Client_PlayerInfoUpdated;
            Destroy(_scoreboardText);
            Instance = null;
        }
    }
}
