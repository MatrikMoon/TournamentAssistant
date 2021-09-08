#pragma warning disable CS0649
#pragma warning disable IDE0044
#pragma warning disable IDE0051
#pragma warning disable IDE0052
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TMPro;
using UnityEngine.UI;

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

        [UIComponent("tournament-button")]
        private Button _tournamentRoomButton;

        [UIComponent("battlesaber-button")]
        private Button _battleSaberButton;

        [UIComponent("bottom-text-panel")]
        private TextMeshProUGUI _bottomTextPanel;

        [UIValue("bottom-text")]
        private string quote = QuoteRandomizer();

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

        public void EnableButtons()
        {
            _tournamentRoomButton.interactable = true;
            _battleSaberButton.interactable = true;
        }

        public void DisableButtons()
        {
            _tournamentRoomButton.interactable = false;
            _battleSaberButton.interactable = false;
        }

        public static string QuoteRandomizer()
        {
            Random rnd = new();
            string[] quotes =
            {
                "It’s hard to beat a person who never gives up.",
                "You're off to great places, today is your day.",
                "Winning doesn't always mean being first.",
                "It always seems impossible until it is done.",
                "The only time you fail is when you fall down and stay down.",
                "It’s not whether you get knocked down, it’s whether you get up.",
                "The difference between ordinary and extraordinary is that little extra.",
                "Success is the sum of small efforts repeated day in and day out.",
                "If you're a true warrior, competition doesn't scare you. It makes you better.",
                "Becoming number one is easier than remaining number one.",
                "In a competition, there's always winners and losers. And I think everyone is here to win, which makes it fun for us all."

                //I guess thats enough for now, need to find more later, remind me if I forget - Arimodu#6469

            };
            return quotes[rnd.Next(0, quotes.Length)];
        }
    }
}
