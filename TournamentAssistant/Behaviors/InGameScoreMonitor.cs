using System.Collections;
using System.Linq;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class InGameScoreMonitor : MonoBehaviour
    {
        public static InGameScoreMonitor Instance { get; set; }

        private ScoreController _scoreController;
        private AudioTimeSyncController _audioTimeSyncController;

        private int _lastScore = 0;
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

                if (_scoreUpdateDelay > 500)
                {
                    _scoreUpdateDelay = 0;
                    ScoreUpdated(_scoreController.prevFrameModifiedScore, _audioTimeSyncController.songTime);
                }
            }
            _scoreUpdateDelay++;
        }

        private void ScoreUpdated(int score, float time)
        {
            //Send score update
            TournamentAssistantShared.Logger.Info($"SENDING UPDATE SCORE: {score}");
            Plugin.client.Self.CurrentScore = score;
            var playerUpdate = new Event();
            playerUpdate.eventType = Event.EventType.PlayerUpdated;
            playerUpdate.changedObject = Plugin.client.Self;
            Plugin.client.Send(new Packet(playerUpdate));
        }

        public IEnumerator WaitForComponentCreation()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<ScoreController>().Any());
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Any());
            _scoreController = Resources.FindObjectsOfTypeAll<ScoreController>().First();
            _audioTimeSyncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().First();

            //_scoreController.noteWasCutEvent += ScoreController_noteWasCutEvent;
            //_scoreController.scoreDidChangeEvent += scoreController_scoreDidChangeEvent;
        }

        /*private void scoreController_scoreDidChangeEvent(int rawScore, int score)
        {
            ScoreUpdated?.Invoke(score, _audioTimeSyncController.songTime);
        }*/

        /*private void ScoreController_noteWasCutEvent(NoteData noteData, NoteCutInfo noteCutInfo, int multiplier)
        {
            if (noteData.noteType == NoteType.NoteA || noteData.noteType == NoteType.NoteB)
            {
                if (noteCutInfo.allIsOK)
                {
                    noteCutInfo.swingRatingCounter.didFinishEvent += SwingRatingCounter_didFinishEvent;
                }
            }
        }

        private void SwingRatingCounter_didFinishEvent(SaberSwingRatingCounter swingRatingCounter)
        {
            swingRatingCounter.didFinishEvent -= SwingRatingCounter_didFinishEvent;

            ScoreUpdated?.Invoke(_scoreController.prevFrameModifiedScore, _audioTimeSyncController.songTime);
        }*/

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            //_scoreController.noteWasCutEvent -= ScoreController_noteWasCutEvent;
            //_scoreController.scoreDidChangeEvent -= scoreController_scoreDidChangeEvent;

            Instance = null;
        }
    }
}
