using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zenject;

namespace TournamentAssistant.Behaviors
{
    class InGameScoreMonitor : MonoBehaviour
    {
        public static InGameScoreMonitor Instance { get; set; }

        public event Action<int, float> ScoreUpdated;

        private ScoreController _scoreController;

        [Inject]
        private AudioTimeSyncController audioTimeSyncController;

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(this); //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
                                     //object is created before the game scene loads, so we need to do this to prevent the game scene
                                     //load from destroying it

            StartCoroutine(WaitForComponentCreation());
        }

        public IEnumerator WaitForComponentCreation()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<ScoreController>().Any());
            _scoreController = Resources.FindObjectsOfTypeAll<ScoreController>().First();

            _scoreController.noteWasCutEvent += ScoreController_noteWasCutEvent;
        }

        private void ScoreController_noteWasCutEvent(NoteData noteData, NoteCutInfo noteCutInfo, int multiplier)
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

            ScoreUpdated?.Invoke(_scoreController.prevFrameModifiedScore, audioTimeSyncController.songTime);
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            _scoreController.noteWasCutEvent -= ScoreController_noteWasCutEvent;

            Instance = null;
        }
    }
}
