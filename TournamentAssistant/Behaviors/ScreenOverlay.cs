using BeatSaberMarkupLanguage;
using IPA.Utilities.Async;
using System.Globalization;
using System.Linq;
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
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _overlayCanvas ??= gameObject.AddComponent(Resources.FindObjectsOfTypeAll<Canvas>().First(x => x.name == "DropdownTableView"));
                _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                _overlayCanvas.overrideSorting = true;
                _overlayCanvas.sortingOrder = Resources.FindObjectsOfTypeAll<Canvas>().Length + 1;

                _overlayImage ??= _overlayCanvas.gameObject.AddComponent<RawImage>();
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

        public void ShowColor(string color)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _overlayCanvas ??= gameObject.AddComponent(Resources.FindObjectsOfTypeAll<Canvas>().First(x => x.name == "DropdownTableView"));
                _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                _overlayCanvas.overrideSorting = true;
                _overlayCanvas.sortingOrder = Resources.FindObjectsOfTypeAll<Canvas>().Length + 1;

                _overlayImage ??= _overlayCanvas.gameObject.AddComponent<RawImage>();
                var imageTransform = _overlayImage.transform as RectTransform;
                imageTransform.SetParent(_overlayCanvas.transform, false);
                _overlayImage.material = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x.name == "UINoGlow");
                _overlayImage.texture = null;

                if (color.StartsWith("#"))
                {
                    color = color.Substring(1);
                }

                byte r = byte.Parse(color.Substring(0, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(color.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(color.Substring(4, 2), NumberStyles.HexNumber);

                _overlayImage.color = new Color32(r, g, b, 255);
            });
        }

        public void SetPngBytes(byte[] pngBytes)
        {
            imageBytes = pngBytes;
        }
    }
}