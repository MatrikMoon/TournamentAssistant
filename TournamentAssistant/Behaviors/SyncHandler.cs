using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TournamentAssistant.Misc;
using System.IO;

namespace TournamentAssistant.Behaviors
{
    class SyncHandler : MonoBehaviour
    {
        public static SyncHandler Instance { get; set; }

        private PauseMenuManager pauseMenuManager;
        private StandardLevelGameplayManager standardLevelGameplayManager;

        private string oldLevelText;

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(this); //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
                                     //object is created before the game scene loads, so we need to do this to prevent the game scene
                                     //load from destroying it

            standardLevelGameplayManager = standardLevelGameplayManager ?? Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();
            pauseMenuManager = pauseMenuManager ?? Resources.FindObjectsOfTypeAll<PauseMenuManager>().First();

            StartCoroutine(PauseOnStart());
        }

        public IEnumerator PauseOnStart()
        {
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<PauseController>("_pauseController").GetProperty<bool>("canPause"));

            var pauseController = standardLevelGameplayManager.GetField<PauseController>("_pauseController");
            pauseController.Pause();
            standardLevelGameplayManager.HandlePauseControllerDidPause();

            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(false);
            oldLevelText = pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text;
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").SetText("Please hold: setting up synchronized streams!");
        }

        public void Resume()
        {
            if (pauseMenuManager.enabled)
            {
                pauseMenuManager.ContinueButtonPressed();
                standardLevelGameplayManager.HandlePauseControllerDidResume();

                pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(true);
                pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(true);
                pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(true);
                pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
                pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
                pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").SetText(oldLevelText);
            }
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Instance = null;
        }
    }
}
