using BS_Utils.Utilities;
using System.Collections;
using System.Linq;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class ScoreMonitor : MonoBehaviour
    {
        public static ScoreMonitor Instance { get; set; }

        private ScoreController _scoreController;
        private AudioTimeSyncController _audioTimeSyncController;

        private string[] destinationPlayers;

        private int _lastScore = 0;
        private int _scoreUpdateFrequency = Plugin.client.State.ServerSettings.ScoreUpdateFrequency;
        private int _scoreUpdateDelay = 0;

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(this); //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
                                     //object is created before the game scene loads, so we need to do this to prevent the game scene
                                     //load from destroying it

            StartCoroutine(WaitForComponentCreation());
        }

        public void Update()
        {
            if (_scoreController != null && _scoreController.prevFrameModifiedScore != _lastScore)
            {
                _lastScore = _scoreController.prevFrameModifiedScore;

                if (_scoreUpdateDelay > _scoreUpdateFrequency)
                {
                    _scoreUpdateDelay = 0;
                    ScoreUpdated(_scoreController.prevFrameModifiedScore, _scoreController.GetField<int>("_combo"), _scoreController.prevFrameModifiedScore / _scoreController.immediateMaxPossibleRawScore, _audioTimeSyncController.songTime);
                }
            }
            _scoreUpdateDelay++;
        }

        private void ScoreUpdated(int score, int combo, float accuracy, float time)
        {
            //Send score update
            (Plugin.client.Self as Player).Score = score;
            (Plugin.client.Self as Player).Combo = combo;
            (Plugin.client.Self as Player).Accuracy = accuracy;
            (Plugin.client.Self as Player).SongPosition = time;
            var playerUpdate = new Event();
            playerUpdate.Type = Event.EventType.PlayerUpdated;
            playerUpdate.ChangedObject = Plugin.client.Self;

            //NOTE:/TODO: We don't needa be blasting the entire server
            //with score updates. This update will only go out to other
            //players in the current match and the coordinator
            Plugin.client.Send(destinationPlayers, new Packet(playerUpdate));
        }

        public IEnumerator WaitForComponentCreation()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<ScoreController>().Any());
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Any());
            _scoreController = Resources.FindObjectsOfTypeAll<ScoreController>().First();
            _audioTimeSyncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().First();

            var match = Resources.FindObjectsOfTypeAll<RoomCoordinator>().FirstOrDefault()?.Match;
            destinationPlayers = match.Players.Select(x => x.Guid).Union(new string[] { match.Leader.Guid }).ToArray(); //We don't wanna be doing this every frame
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Instance = null;
        }
    }
}
