using BeatSaberMarkupLanguage;
using IPA.Utilities.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using TournamentAssistantShared.Models;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class FloatingScoreScreen : MonoBehaviour
    {
        public static FloatingScoreScreen Instance { get; set; }

        private PluginClient Client { get; set; }
        private Match Match { get; set; }
        private Tournament Tournament { get; set; }

        private List<(User, RealtimeScore)> _scores;
        private TextMeshProUGUI _scoreboardText;

        public void SetClient(PluginClient client)
        {
            Client = client;
        }

        public void SetMatch(Match match)
        {
            Match = match;
        }

        public void SetTournament(Tournament tournament)
        {
            Tournament = tournament;
        }

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

            // Set initial scores (all zero, just getting player list really)
            var players = Match.AssociatedUsers.Select(x => Client.StateManager.GetUser(Tournament.Guid, x)).Where(x => x.ClientType == User.ClientTypes.Player);
            _scores = players.Select(x => (x, new RealtimeScore())).ToList();

            Client.RealtimeScoreReceived += Client_RealtimeScoreReceived;
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

                UnityMainThreadTaskScheduler.Factory.StartNew(() => _scoreboardText.SetText(leaderboardText));
            }
            return Task.CompletedTask;
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Client.RealtimeScoreReceived -= Client_RealtimeScoreReceived;
            Destroy(_scoreboardText);
            Instance = null;
        }
    }
}
