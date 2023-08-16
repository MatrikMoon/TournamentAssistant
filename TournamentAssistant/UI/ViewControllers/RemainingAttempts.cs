#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using TMPro;

namespace TournamentAssistant.UI.ViewControllers
{
    [HotReload(RelativePathToLayout = @"RemainingAttempts.bsml")]
    internal class RemainingAttempts : BSMLAutomaticViewController
    {
        [UIComponent("text")]
        private TextMeshProUGUI text;

        [UIValue("text-string")]
        private string _text;

        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
                if (text != null)
                {
                    text.richText = true;
                    text.text = _text;
                }
            }
        }

        public void SetRemainingAttempts(int attempts)
        {
            if (attempts > 0)
            {
                text.color = new UnityEngine.Color(0, 1, 0);
            }
            else
            {
                text.color = new UnityEngine.Color(1, 0, 0);
            }

            Text = $"Remaining Attempts: {attempts}";
        }
    }
}
