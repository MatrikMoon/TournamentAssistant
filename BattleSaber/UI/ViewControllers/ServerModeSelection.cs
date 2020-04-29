#pragma warning disable 0649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TMPro;
using BattleSaberShared;
using UnityEngine.UI;

namespace BattleSaber.UI.ViewControllers
{
    class ServerModeSelection : BSMLResourceViewController
    {
        public override string ResourceName => $"BattleSaber.UI.Views.{GetType().Name}.bsml";

        public event Action TournamentButtonPressed;
        public event Action BattleSaberButtonPressed;

        [UIComponent("status-text")]
        private TextMeshProUGUI statusText;

        [UIComponent("tournament-button")]
        private Button _tournamentRoomButton;

        [UIComponent("battlesaber-button")]
        private Button _battleSaberButton;

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

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);
            if (type == ActivationType.AddedToHierarchy)
            {
                if (_statusText != null) statusText.text = _statusText;
                _tournamentRoomButton.interactable = false;
                _battleSaberButton.interactable = false;
            }
        }

        [UIAction("tournament-button-pressed")]
        private void TournamentButtonPress()
        {
            TournamentButtonPressed?.Invoke();
        }

        [UIAction("battlesaber-button-pressed")]
        private void BattleSaberButtonPress()
        {
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
    }
}
