using HMUI;
using System;
using UnityEngine.UI;

namespace TournamentAssistant.UI.NavigationControllers
{
    class GeneralNavigationController : NavigationController
    {
        private Button _backButton;
        public event Action<GeneralNavigationController> didFinishEvent;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                //_backButton = BeatSaberUI.CreateBackButton(rectTransform, () => didFinishEvent?.Invoke(this));
            }
        }
    }
}