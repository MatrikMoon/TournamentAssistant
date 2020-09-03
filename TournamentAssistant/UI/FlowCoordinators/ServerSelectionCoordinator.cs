using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Collections.Generic;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ServerSelectionCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        public FlowCoordinatorWithClient DestinationCoordinator { get; set; }

        private ServerSelection _serverSelectionViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Server Selection Screen";
                showBackButton = true;

                //Load server list from config
                var servers = new List<CoreServer>(Plugin.config.GetServers());
                if (servers.Count <= 0)
                {
                    servers.Clear();
                    servers.Add(
                        new CoreServer()
                        {
                            Name = "Moon's Server",
                            Address = "beatsaber.networkauditor.org",
                            Port = 10156
                        }
                    );

                    Plugin.config.SaveServers(servers.ToArray());
                }

                _serverSelectionViewController = BeatSaberUI.CreateViewController<ServerSelection>();
                _serverSelectionViewController.SetServers(new List<CoreServer>(servers));
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

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DidFinishEvent?.Invoke();
        }

        private void serverSelectionViewController_selectedServer(CoreServer host)
        {
            DestinationCoordinator.DidFinishEvent += destinationCoordinator_DidFinishEvent;
            DestinationCoordinator.Host = host;
            PresentFlowCoordinator(DestinationCoordinator);
        }

        private void destinationCoordinator_DidFinishEvent()
        {
            DestinationCoordinator.DidFinishEvent -= destinationCoordinator_DidFinishEvent;
            DismissFlowCoordinator(DestinationCoordinator);
        }
    }
}
