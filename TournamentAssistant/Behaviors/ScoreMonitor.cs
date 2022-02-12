using System;
using System.Collections;
using System.Linq;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utillities;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class ScoreMonitor : MonoBehaviour
    {
        public static ScoreMonitor Instance { get; set; }

        private ScoreController _scoreController;
        private AudioTimeSyncController _audioTimeSyncController;

        private Guid[] destinationPlayers;

        private int _lastScore = 0;
        private int _scoreUpdateFrequency = Plugin.client.State.ServerSettings.ScoreUpdateFrequency;
        private int _scoreCheckDelay = 0;

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(
                this); //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
            //object is created before the game scene loads, so we need to do this to prevent the game scene
            //load from destroying it

            StartCoroutine(WaitForComponentCreation());
        }

        public void Update()
        {
            if (_scoreCheckDelay > _scoreUpdateFrequency)
            {
                _scoreCheckDelay = 0;

                if (_scoreController != null && _scoreController.prevFrameModifiedScore != _lastScore)
                {
                    _lastScore = _scoreController.prevFrameModifiedScore;

                    ScoreUpdated(_scoreController.prevFrameModifiedScore, _scoreController.GetField<int>("_combo"),
                        (float) _scoreController.prevFrameModifiedScore / _scoreController.immediateMaxPossibleRawScore,
                        _audioTimeSyncController.songTime);
                }
            }

            _scoreCheckDelay++;
        }

        private void ScoreUpdated(int score, int combo, float accuracy, float time)
        {
            //Send score update
            var player = Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
            player.Score = score;
            player.Combo = combo;
            player.Accuracy = accuracy;
            player.SongPosition = time;
            var playerUpdate = new Event
            {
                player_updated_event = new Event.PlayerUpdatedEvent
                {
                    Player = player
                }
            };

            //NOTE:/TODO: We don't needa be blasting the entire server
            //with score updates. This update will only go out to other
            //players in the current match and the coordinator
            Plugin.client.Send(destinationPlayers, new Packet
            {
                Event = playerUpdate
            });
        }

        public IEnumerator WaitForComponentCreation()
        {
            var coordinator = Resources.FindObjectsOfTypeAll<RoomCoordinator>().FirstOrDefault();
            var match = coordinator?.Match;
            destinationPlayers = ((bool) (coordinator?.TournamentMode) && !Plugin.UseFloatingScoreboard)
                ? new Guid[] {Guid.Parse(match.Leader.Id)}
                : match.Players.Select(x => Guid.Parse(x.User.Id)).Union(new Guid[] {Guid.Parse(match.Leader.Id)})
                    .ToArray(); //We don't wanna be doing this every frame
            //new string[] { "x_x" }; //Note to future moon, this will cause the server to receive the forwarding packet and forward it to no one. Since it's received, though, the scoreboard will get it if connected

            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<ScoreController>().Any());
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Any());
            _scoreController = Resources.FindObjectsOfTypeAll<ScoreController>().First();
            _audioTimeSyncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().First();
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy() => Instance = null;
    }
}