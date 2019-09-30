using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace TournamentAssistant.UI.ViewControllers
{
    class IntroViewController : BSMLResourceViewController
    {
        public override string ResourceName => "TournamentAssistant.UI.Views.IntroView.bsml";

        [UIComponent("status-text")]
        private TextMeshProUGUI statusText;

        public void SetStatusText(string text)
        {
            if (statusText != null) statusText.text = text;
        }
    }
}
