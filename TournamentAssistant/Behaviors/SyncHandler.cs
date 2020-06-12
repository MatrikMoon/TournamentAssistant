using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

            standardLevelGameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();
            pauseMenuManager = Resources.FindObjectsOfTypeAll<PauseMenuManager>().First();

            StartCoroutine(PauseOnStart());
        }

        public IEnumerator PauseOnStart()
        {
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<PauseController>("_pauseController").GetProperty<bool>("canPause"));

            //Prevent players from unpausing with their menu buttons
            var pauseController = standardLevelGameplayManager.GetField<PauseController>("_pauseController");
            pauseMenuManager.didPressContinueButtonEvent -= pauseController.HandlePauseMenuManagerDidPressContinueButton;

            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(false);
            oldLevelText = pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text;
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text = "Please hold: setting up synchronized streams!";

            //If we've disabled pause, we need to reenable it so we can pause for stream sync
            if (Plugin.DisablePause) pauseController.canPauseEvent += standardLevelGameplayManager.HandlePauseControllerCanPause;

            pauseController.Pause();
            standardLevelGameplayManager.HandlePauseControllerDidPause();
        }

        public void Resume()
        {
            var pauseController = standardLevelGameplayManager.GetField<PauseController>("_pauseController");
            var pauseMenuManager = pauseController.GetField<PauseMenuManager>("_pauseMenuManager");

            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text = oldLevelText;

            //Allow players to unpause in the future
            pauseMenuManager.didPressContinueButtonEvent += pauseController.HandlePauseMenuManagerDidPressContinueButton;

            pauseMenuManager.ContinueButtonPressed();
            standardLevelGameplayManager.HandlePauseControllerDidResume();

            //If we've disabled pause, we need to disable it again since we reenabled it earlier
            if (Plugin.DisablePause) pauseController.canPauseEvent -= standardLevelGameplayManager.HandlePauseControllerCanPause;
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Instance = null;
        }
    }
}
