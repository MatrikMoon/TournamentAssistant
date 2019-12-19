using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TournamentAssistantShared;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.Behaviors
{
    class InGameSyncController : MonoBehaviour
    {
        public static InGameSyncController Instance { get; set; }

        private PauseMenuManager pauseMenuManager;
        private StandardLevelGameplayManager standardLevelGameplayManager;

        private string oldLevelText;
        private Canvas _colorCanvas;
        private RawImage _colorImage;

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
            pauseMenuManager.ContinueButtonPressed();
            standardLevelGameplayManager.HandlePauseControllerDidResume();

            pauseMenuManager.GetField<Button>("_restartButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_continueButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<Button>("_backButton").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_beatmapDifficultyText").gameObject.SetActive(true);
            pauseMenuManager.GetField<TextMeshProUGUI>("_levelNameText").SetText(oldLevelText);

            _colorImage.color = Color.clear;
        }

        public void TriggerColorChange()
        {
            _colorCanvas = gameObject.AddComponent<Canvas>();
            _colorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasTransform = _colorCanvas.transform as RectTransform;
            canvasTransform.anchorMin = new Vector2(1, 0);
            canvasTransform.anchorMax = new Vector2(0, 1);
            canvasTransform.pivot = new Vector2(0.5f, 0.5f);

            _colorImage = _colorCanvas.gameObject.AddComponent<RawImage>();
            var imageTransform = _colorImage.transform as RectTransform;
            imageTransform.SetParent(_colorCanvas.transform, false);
            _colorImage.color = new Color32(0, 128, 0, 255);
            _colorImage.material = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x.name == "UINoGlow");
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Instance = null;
        }
    }
}
