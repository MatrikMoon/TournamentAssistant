using BeatSaberMarkupLanguage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class FloatingScoreScreen : MonoBehaviour
    {
        public static FloatingScoreScreen Instance { get; set; }

        private List<(User, RealtimeScore)> _scores;
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
            var roomCoordinator = Resources.FindObjectsOfTypeAll<RoomCoordinator>().FirstOrDefault();
            var tournamentId = roomCoordinator?.TournamentId;
            var match = roomCoordinator?.Match;

            //Set initial scores (all zero, just getting player list really)
            var players = match.AssociatedUsers.Select(x => Plugin.client.StateManager.GetUser(tournamentId, x)).Where(x => x.ClientType == User.ClientTypes.Player);
            _scores = players.Select(x => (x, new RealtimeScore())).ToList();

            Plugin.client.RealtimeScoreReceived += Client_RealtimeScoreReceived;
        }

        private Task Client_RealtimeScoreReceived(RealtimeScore score)
        {
            if (_scores.Select(x => x.Item1.Guid).Contains(score.UserGuid))
            {
                _scores.First(x => x.Item1.Guid == score.UserGuid).Item2.ScoreWithModifiers = score.ScoreWithModifiers;
                var leaderboard = _scores.OrderByDescending(x => x.Item2.ScoreWithModifiers).Take(5);

                var index = 1;
                var leaderboardText = string.Empty;
                foreach (var leaderboardPlayer in leaderboard) leaderboardText += $"{index++}: {leaderboardPlayer.Item1.Name} - {leaderboardPlayer.Item2.ScoreWithModifiers}\n";

                UnityMainThreadDispatcher.Instance().Enqueue(() => _scoreboardText.SetText(leaderboardText));
            }
            return Task.CompletedTask;
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Plugin.client.RealtimeScoreReceived -= Client_RealtimeScoreReceived;
            Destroy(_scoreboardText);
            Instance = null;
        }
    }
}
