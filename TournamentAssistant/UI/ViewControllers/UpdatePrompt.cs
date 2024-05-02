#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using IPA.Utilities.Async;
using System;
using System.Threading.Tasks;
using TMPro;
using TournamentAssistant.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.ViewControllers
{
    [HotReload(RelativePathToLayout = @"UpdatePrompt.bsml")]
    internal class UpdatePrompt : BSMLAutomaticViewController
    {
        public event Action Cancel;

        [UIValue("update-title-text")]
        private string updateTitleText = "Update Available";

        [UIValue("update-warning-text")]
        private string updateWarningText = "An update is available! Would you like to download it now?\nYour game will be restarted once the download is complete.";

        [UIComponent("progress-image")]
        private RawImage progressImage;

        [UIComponent("progress-text")]
        private TextMeshProUGUI progressText;

        [UIValue("cancel-text")]
        private string cancelText = "Cancel";

        [UIValue("update-text")]
        private string updateText = "Update";

        [UIObject("background")]
        internal GameObject background = null;

        [UIObject("buttons-container")]
        internal GameObject buttonsContainer = null;

        private static float defaultHeight = 10f;
        private static float defaultWidth = 60f;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            BackgroundOpacity();

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);

            progressText.text = "";
            progressText.color = new Color(0.65f, 0.65f, 0.65f, 1f);

            progressImage.color = new Color(1, 1, 1, 0.3f);
            progressImage.texture = texture;
            progressImage.rectTransform.sizeDelta = new Vector2(0, defaultHeight);
        }

        private void OnDownloadProgress(double progress)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                SetProgress(progress);
            });
        }

        private void SetProgress(double progress)
        {
            if (progressImage != null)
            {
                progressImage.rectTransform.sizeDelta = new Vector2(defaultWidth * (float)(progress / 100), defaultHeight);
            }

            progressText.text = $"{progress}%";
        }

        [UIAction("cancel")]
        public void OnCancelClicked()
        {
            Cancel?.Invoke();
        }

        [UIAction("update")]
        public Task OnUpdateClicked()
        {
            buttonsContainer.SetActive(false);
            return Updater.Update(OnDownloadProgress);
        }

        void BackgroundOpacity()
        {
            var image = background?.GetComponent<HMUI.ImageView>() ?? null;
            var color = image.color;
            color.a = 0.5f;
            image.color = color;
        }
    }
}
