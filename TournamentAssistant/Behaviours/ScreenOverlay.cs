using IPA.Utilities.Async;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.Behaviours
{
    internal class ScreenOverlay : MonoBehaviour
    {
        private Canvas _overlayCanvas = null!;
        private RawImage _overlayImage = null!;
        private byte[] imageBytes = Array.Empty<byte>();

        protected void Awake()
        {
            _overlayCanvas = gameObject.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayImage = gameObject.AddComponent<RawImage>();
            _overlayCanvas.sortingOrder = 5001;

            _overlayImage.rectTransform.SetParent(_overlayCanvas.transform, false);
            _overlayImage.material = BeatSaberMarkupLanguage.Utilities.ImageResources.NoGlowMat;
            _overlayImage.color = Color.clear;
            _overlayCanvas.enabled = false;
            _overlayImage.enabled = false;
        }

        public void ShowPng()
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                if (imageBytes != null)
                {
                    _overlayImage.enabled = true;
                    _overlayCanvas.enabled = true;
                    var texture = new Texture2D(1, 1);
                    ImageConversion.LoadImage(texture, imageBytes);
                    _overlayImage.color = Color.white;
                    _overlayImage.texture = texture;
                }
            });
        }

        public void Clear()
        {
            if (_overlayImage != null)
            {
                _overlayImage.color = Color.clear;
                _overlayCanvas.enabled = false;
                _overlayImage.enabled = false;
            }

        }

        public void SetPngBytes(byte[] pngBytes)
        {
            imageBytes = pngBytes;
        }
    }
}