#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using TMPro;

namespace BattleSaber.UI.ViewControllers
{
    class SplashScreen : BSMLResourceViewController
    {
        public override string ResourceName => $"BattleSaber.UI.Views.{GetType().Name}.bsml";

        [UIComponent("status-text")]
        private TextMeshProUGUI statusText;

        [UIValue("status-text-string")]
        private string _statusText;
        public string StatusText
        {
            get
            {
                return _statusText;
            }
            set
            {
                _statusText = value;
                if (statusText != null) statusText.text = _statusText;
            }
        }
    }
}