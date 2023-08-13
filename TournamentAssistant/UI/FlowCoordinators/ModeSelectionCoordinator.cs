/*using BeatSaberMarkupLanguage;
using HMUI;
using System;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ModeSelectionCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        public CoreServer Server { get; set; }

        private ServerModeSelection _serverModeSelectionViewController;
        private PatchNotes _patchNotesViewController;
        private ServerMessage _serverMessage;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                SetTitle($"TournamentAssistant v{Constants.VERSION}");
                showBackButton = true;

                _patchNotesViewController = BeatSaberUI.CreateViewController<PatchNotes>();
                _serverModeSelectionViewController = BeatSaberUI.CreateViewController<ServerModeSelection>();
                _serverModeSelectionViewController.BattleSaberButtonPressed += ServerModeSelectionViewController_BattleSaberButtonPressed;
                _serverModeSelectionViewController.QualifierButtonPressed += ServerModeSelectionViewController_QualifierButtonPressed;
                _serverModeSelectionViewController.TournamentButtonPressed += ServerModeSelectionViewController_TournamentButtonPressed;

                ProvideInitialViewControllers(_serverModeSelectionViewController, null, _patchNotesViewController);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);

            DidFinishEvent?.Invoke();
        }

        private void ServerModeSelectionViewController_BattleSaberButtonPressed()
        {
            *//*_serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<TournamentSelectionCoordinator>();
            _serverSelectionCoordinator.DestinationCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomSelectionCoordinator>();
            _serverSelectionCoordinator.DidFinishEvent += ServerSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_serverSelectionCoordinator);*//*
        }

        private void ServerModeSelectionViewController_QualifierButtonPressed()
        {
            *//*_eventSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<EventSelectionCoordinator>();
            _eventSelectionCoordinator.DidFinishEvent += EventSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_eventSelectionCoordinator);*//*
        }

        private void ServerModeSelectionViewController_TournamentButtonPressed()
        {
            *//*BeatSaberUI.CreateFlowCoordinator<RoomCoordinator>();
            _serverSelectionCoordinator.DidFinishEvent += ServerSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_serverSelectionCoordinator);*//*
        }

        *//*private void ServerSelectionCoordinator_DidFinishEvent()
        {
            _serverSelectionCoordinator.DidFinishEvent -= ServerSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_serverSelectionCoordinator);
        }*/

        /*private void EventSelectionCoordinator_DidFinishEvent()
        {
            _eventSelectionCoordinator.DidFinishEvent -= EventSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_eventSelectionCoordinator);
        }*//*
    }
}
*/