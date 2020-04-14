#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using TMPro;

namespace TournamentAssistant.UI.ViewControllers
{
    class IntroViewController : BSMLResourceViewController
    {
        public override string ResourceName => "TournamentAssistant.UI.Views.IntroView.bsml";

        [UIComponent("status-text")]
        private TextMeshProUGUI statusText;

        //We need to keep track of matches like this because it is very possible
        //that we'll want to add a match to the list and that logic will come through
        //before the list is actually displayed. This way, we can handle that situation
        //and avoid null exceptions / missing data
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

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);
            if (type == ActivationType.AddedToHierarchy)
            {
                if (_statusText != null) statusText.text = _statusText;
            }
        }
    }
}
