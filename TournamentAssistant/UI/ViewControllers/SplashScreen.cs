#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using TMPro;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class SplashScreen : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

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
                if (statusText != null)
                {
                    statusText.richText = true;
                    statusText.text = _statusText;
                }
            }
        }
    }
}
