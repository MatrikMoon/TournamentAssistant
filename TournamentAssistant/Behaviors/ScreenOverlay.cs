using System.Linq;
using TournamentAssistant.Misc;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.Behaviors
{
    class ScreenOverlay : MonoBehaviour
    {
        public static ScreenOverlay Instance { get; set; }

        private Canvas _overlayCanvas;
        private RawImage _overlayImage;
        private byte[] imageBytes;

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }

        public void Clear()
        {
            if (_overlayImage != null) _overlayImage.color = Color.clear;
        }

        public void ShowPng()
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
                    _overlayImage.color = Color.white;
                    _overlayImage.texture = texture;
                }
            });
        }

        public void SetPngBytes(byte[] pngBytes)
        {
            imageBytes = pngBytes;
        }
    }
}
