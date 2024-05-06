#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using TMPro;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    [HotReload(RelativePathToLayout = @"SplashScreen.bsml")]
    internal class SplashScreen : BSMLAutomaticViewController
    {
        [UIObject("splash-background")]
        private GameObject splashBackground = null;

        [UIComponent("status-text")]
        private TextMeshProUGUI statusText;

        [UIValue("status-text-string")]
        private string _statusText;

        [UIComponent("title-text")]
        private TextMeshProUGUI titleText;

        [UIValue("title-text-string")]
        private string _titleText;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            BackgroundOpacity();
        }

        public string TitleText
        {
            get
            {
                return _titleText;
            }
            set
            {
                _titleText = value;
                if (titleText != null)
                {
                    titleText.richText = true;
                    titleText.text = _titleText;
                }
            }
        }

        public string StatusText
        {
            get
            {
                return _statusText;
            }
            set
            {
                _statusText = value;
                if (statusText != null)
                {
                    statusText.richText = true;
                    statusText.text = _statusText;
                }
            }
        }

        void BackgroundOpacity()
        {
            var image = splashBackground?.GetComponent<HMUI.ImageView>() ?? null;
            var color = image.color;
            color.a = 0.5f;
            image.color = color;
        }
    }
}
