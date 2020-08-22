using System.Collections;
using System.Linq;
using TMPro;
using TournamentAssistant.Misc;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.Behaviors
{
    class SyncHandler : MonoBehaviour
    {
        public static SyncHandler Instance { get; set; }

        private PauseMenuManager pauseMenuManager;
        private PauseController pauseController;
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
            pauseController = Resources.FindObjectsOfTypeAll<PauseController>().First();

            StartCoroutine(PauseOnStart());
        }

        public IEnumerator PauseOnStart()
        {
            //If we've disabled pause, we need to reenable it so we can pause for stream sync
            if (Plugin.DisablePause)
            {
                //We know pausecontroller will be guaranteed true here since we've already waited for it when disabling pause
                var guaranteedPauseController = pauseController;
                guaranteedPauseController.canPauseEvent -= AntiPause.HandlePauseControllerCanPause_AlwaysFalse;
                guaranteedPauseController.canPauseEvent += standardLevelGameplayManager.HandlePauseControllerCanPause;
            }
            else
            {
                yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
                yield return new WaitUntil(() => standardLevelGameplayManager.GetField<PauseController>("_pauseController").GetProperty<bool>("canPause"));
            }

            //Prevent players from unpausing with their menu buttons
            pauseMenuManager.didPressContinueButtonEvent -= pauseController.HandlePauseMenuManagerDidPressContinueButton;

            pauseController.Pause();

            //Wait for the pauseMenuManager to have started and set the pause menu text
            //The text we're checking for is the default text for that field
            yield return new WaitUntil(() => pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text != @"Super Long Song Name\n<size=80%>ft great artist</size>");

            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(false);
            oldLevelText = pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text;
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text = "Please wait!\n<size=80%>Setting up synchronized streams!</size>";

        }

        public void Resume()
        {
            var pauseMenuManager = pauseController.GetField<PauseMenuManager>("_pauseMenuManager");

            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text = oldLevelText;

            //Allow players to unpause in the future
            pauseMenuManager.didPressContinueButtonEvent += pauseController.HandlePauseMenuManagerDidPressContinueButton;

            //Resume the game
            pauseMenuManager.ContinueButtonPressed();

            //If we've disabled pause, we need to disable it again since we reenabled it earlier
            if (Plugin.DisablePause)
            {
                pauseController.canPauseEvent -= standardLevelGameplayManager.HandlePauseControllerCanPause;
                pauseController.canPauseEvent += AntiPause.HandlePauseControllerCanPause_AlwaysFalse;
            }
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Instance = null;
        }
    }
}