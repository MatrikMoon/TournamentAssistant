using HMUI;
using System;
using System.Collections.Generic;
using TournamentAssistant.Models;
using TournamentAssistant.UI.ViewControllers;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ServerSelectionCoordinator : FlowCoordinator
    {
        public event Action DidFinishEvent;

        private ServerModeSelectionCoordinator _modeSelectionCoordinator;
        private ServerSelection _serverSelectionViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Server Selection Screen";
                showBackButton = true;

                _serverSelectionViewController = _serverSelectionViewController ?? BeatSaberUI.CreateViewController<ServerSelection>();
                _serverSelectionViewController.SetRooms(
                    new List<CoreServer>() {
                        new CoreServer() {
                            Name = "Moon's Server",
                            Address = "beatsaber.networkauditor.org"
                        }
                    }
                );
                _serverSelectionViewController.ServerSelected += serverSelectionViewController_selectedServer;

                ProvideInitialViewControllers(_serverSelectionViewController);
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                _serverSelectionViewController.ServerSelected -= serverSelectionViewController_selectedServer;
            }
        }

        private void serverSelectionViewController_selectedServer(CoreServer host)
        {
            _modeSelectionCoordinator = _modeSelectionCoordinator ?? BeatSaberUI.CreateFlowCoordinator<ServerModeSelectionCoordinator>(gameObject);
            _modeSelectionCoordinator.DidFinishEvent += modeSelectionCoordinator_DidFinishEvent;
            _modeSelectionCoordinator.Host = host;
            PresentFlowCoordinator(_modeSelectionCoordinator);
        }

        private void modeSelectionCoordinator_DidFinishEvent()
        {
            _modeSelectionCoordinator.DidFinishEvent -= modeSelectionCoordinator_DidFinishEvent;

            DismissFlowCoordinator(_modeSelectionCoordinator);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DidFinishEvent?.Invoke();
        }
    }
}
