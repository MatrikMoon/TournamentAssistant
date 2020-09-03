using BeatSaberMarkupLanguage;
using HMUI;
using System;
using TournamentAssistant.UI.ViewControllers;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ServerModeSelectionCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        private EventSelectionCoordinator _eventSelectionCoordinator;
        private ServerSelectionCoordinator _serverSelectionCoordinator;
        private ServerModeSelection _serverModeSelectionViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                title = "Choose Your Path";
                showBackButton = true;

                _serverModeSelectionViewController = BeatSaberUI.CreateViewController<ServerModeSelection>();
                _serverModeSelectionViewController.BattleSaberButtonPressed += serverModeSelectionViewController_BattleSaberButtonPressed;
                _serverModeSelectionViewController.QualifierButtonPressed += serverModeSelectionViewController_QualifierButtonPressed;
                _serverModeSelectionViewController.TournamentButtonPressed += serverModeSelectionViewController_TournamentButtonPressed;

                ProvideInitialViewControllers(_serverModeSelectionViewController);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DidFinishEvent?.Invoke();
        }

        private void serverModeSelectionViewController_BattleSaberButtonPressed()
        {
            _serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ServerSelectionCoordinator>();
            _serverSelectionCoordinator.DestinationCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomSelectionCoordinator>();
            _serverSelectionCoordinator.DidFinishEvent += serverSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_serverSelectionCoordinator);
        }

        private void serverModeSelectionViewController_QualifierButtonPressed()
        {
            _eventSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<EventSelectionCoordinator>();
            _eventSelectionCoordinator.DidFinishEvent += eventSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_eventSelectionCoordinator);
        }

        private void serverModeSelectionViewController_TournamentButtonPressed()
        {
            _serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ServerSelectionCoordinator>();
            _serverSelectionCoordinator.DestinationCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomCoordinator>();
            _serverSelectionCoordinator.DidFinishEvent += serverSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_serverSelectionCoordinator);
        }

        private void serverSelectionCoordinator_DidFinishEvent()
        {
            _serverSelectionCoordinator.DidFinishEvent -= serverSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_serverSelectionCoordinator);
        }

        private void eventSelectionCoordinator_DidFinishEvent()
        {
            _eventSelectionCoordinator.DidFinishEvent -= eventSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_eventSelectionCoordinator);
        }
    }
}
