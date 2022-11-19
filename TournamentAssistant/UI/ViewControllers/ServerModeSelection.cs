#pragma warning disable CS0649
#pragma warning disable IDE0044
#pragma warning disable IDE0051
#pragma warning disable IDE0052
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TMPro;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class ServerModeSelection : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action TournamentButtonPressed;
        public event Action QualifierButtonPressed;
        public event Action BattleSaberButtonPressed;

        [UIComponent("status-text")]
        private TextMeshProUGUI statusText;

        [UIComponent("bottom-text-panel")]
        private TextMeshProUGUI _bottomTextPanel;

        [UIValue("bottom-text")]
        private string quote = QuoteRandomizer();

        [UIValue("tournament-text")]
        private string tournamentText = Plugin.GetLocalized("tournament");

        [UIValue("tournament-hint-text")]
        private string tournamentHintText = Plugin.GetLocalized("tournament_hint");

        [UIValue("qualifiers-text")]
        private string qualifiersText = Plugin.GetLocalized("qualifiers");

        [UIValue("qualifiers-hint-text")]
        private string qualifiersHintText = Plugin.GetLocalized("qualifiers_hint");

        //We need to keep track of the text like this because it is very possible
        //that we'll want to update it before the list is actually displayed.
        //This way, we can handle that situation and avoid null exceptions / missing data
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

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (addedToHierarchy)
            {
                if (_statusText != null) statusText.text = _statusText;
            }
        }

        [UIAction("tournament-button-pressed")]
        private void TournamentButtonPress()
        {
            _bottomTextPanel.text = QuoteRandomizer();
            TournamentButtonPressed?.Invoke();
        }

        [UIAction("qualifier-button-pressed")]
        private void QualifierButtonPress()
        {
            _bottomTextPanel.text = QuoteRandomizer();
            QualifierButtonPressed?.Invoke();
        }

        [UIAction("battlesaber-button-pressed")]
        private void BattleSaberButtonPress()
        {
            _bottomTextPanel.text = QuoteRandomizer();
            BattleSaberButtonPressed?.Invoke();
        }

        public static string QuoteRandomizer()
        {
            var rnd = new Random();
            var quotes = Plugin.GetQuotes();
            return quotes[rnd.Next(0, quotes.Length)];
        }
    }
}
