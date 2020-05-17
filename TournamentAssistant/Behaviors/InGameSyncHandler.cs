using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TournamentAssistant.Misc;
using System.IO;

namespace TournamentAssistant.Behaviors
{
    class InGameSyncHandler : MonoBehaviour
    {
        public static InGameSyncHandler Instance { get; set; }

        private PauseMenuManager pauseMenuManager;
        private StandardLevelGameplayManager standardLevelGameplayManager;

        private string oldLevelText;
        private Canvas _overlayCanvas;
        private RawImage _overlayImage;
        private byte[] imageBytes;

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
            ClearBackground();

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

        public void ClearBackground()
        {
            if (_overlayImage != null) _overlayImage.color = Color.clear;
        }

        public void ShowSetImage()
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _overlayCanvas = _overlayCanvas ?? gameObject.AddComponent<Canvas>();
                _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var canvasTransform = _overlayCanvas.transform as RectTransform;
                canvasTransform.anchorMin = new Vector2(1, 0);
                canvasTransform.anchorMax = new Vector2(0, 1);
                canvasTransform.pivot = new Vector2(0.5f, 0.5f);

                _overlayImage = _overlayImage ?? _overlayCanvas.gameObject.AddComponent<RawImage>();
                var imageTransform = _overlayImage.transform as RectTransform;
                imageTransform.SetParent(_overlayCanvas.transform, false);
                _overlayImage.material = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x.name == "UINoGlow");

                if (imageBytes != null)
                {
                    var texture = new Texture2D(1, 1);
                    ImageConversion.LoadImage(texture, imageBytes);
                    imageBytes = null;
                    _overlayImage.texture = texture;
                }
                else
                {
                    var texture = new Texture2D(1, 1);
                    texture.SetPixel(0, 0, Color.white);

                    _overlayImage.texture = texture;
                    _overlayImage.color = new Color32(128, 0, 0, 255);
                }
            });
        }

        public void SetPngToUse(byte[] pngBytes)
        {
            imageBytes = pngBytes;
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Instance = null;
        }
    }
}
