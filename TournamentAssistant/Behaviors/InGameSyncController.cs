using System.Linq;
using TMPro;
using TournamentAssistant.Misc;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace TournamentAssistant.Behaviors
{
    class InGameSyncController : MonoBehaviour
    {
        public static InGameSyncController Instance { get; set; }

        [Inject]
        private GamePauseManager gamePauseManager;

        [Inject]
        private PauseMenuManager pauseMenuManager;

        [Inject]
        private AudioTimeSyncController audioTimeSyncController;

        [Inject]
        private VRControllersInputManager controllersInputManager;

        //[Inject]
        private StandardLevelGameplayManager standardLevelGameplayManager;

        private string oldLevelText;

        void Awake()
        {
            Instance = this;

            standardLevelGameplayManager = standardLevelGameplayManager ?? Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();

            standardLevelGameplayManager.HandlePauseTriggered();
            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(false);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(false);
            oldLevelText = pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").text;
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").SetText("Please hold: setting up synchronized streams!");
        }

        public void Resume()
        {
            standardLevelGameplayManager.HandlePauseMenuDidFinishWithContinue();
            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").SetText(oldLevelText);
        }

        public void TriggerColorChange()
        {

        }

        void OnDestroy()
        {
            Instance = null;
        }
    }
}
