using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using SiraUtil.Zenject;
using System;
using UnityEngine.UI;
using Zenject;

namespace TournamentAssistant.ViewControllers
{
    [HotReload(RelativePathToLayout = @"..\Views\server-mode-selection-view.bsml")]
    [ViewDefinition("TournamentAssistant.Views.server-mode-selection-view.bsml")]
    internal class ServerModeSelectionView : BSMLAutomaticViewController
    {
        [Inject]
        protected readonly UBinder<Plugin, Random> _random = null!;

        [UIComponent("tournament-button")]
        protected readonly Button _tournamentButton = null!;

        [UIComponent("qualifiers-button")]
        protected readonly Button _qualifiersButton = null!;

        [UIComponent("battle-saber-button")]
        protected readonly Button _battleSaberButton = null!;

        [UIComponent("status-text")]
        protected readonly CurvedTextMeshPro _statusText = null!;

        [UIComponent("bottom-text")]
        protected readonly CurvedTextMeshPro _bottomText = null!;

        public event Action? TournamentClicked;
        public event Action? QualifiersClicked;
        public event Action? BattleSaberClicked;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _bottomText.text = RandomizedQuote();
        }

        public void SetButtonInteractability(bool status)
        {
            _tournamentButton.interactable = status;
            _qualifiersButton.interactable = status;
            _battleSaberButton.interactable = status;
        }

        [UIAction("tournament-clicked")]
        protected void TournamentButtonClicked() => TournamentClicked?.Invoke();

        [UIAction("qualifiers-clicked")]
        protected void QualifiersButtonClicked() => QualifiersClicked?.Invoke();

        [UIAction("battle-saber-clicked")]
        protected void BattleSaberButtonClicked() => BattleSaberClicked?.Invoke();

        private string RandomizedQuote()
        {
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
            return quotes[_random.Value.Next(0, quotes.Length)];
        }
    }
}