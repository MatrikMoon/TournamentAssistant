using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Collections.Generic;
using BattleSaber.Models;
using BattleSaber.UI.ViewControllers;

namespace BattleSaber.UI.FlowCoordinators
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

                //Load server list from config
                var servers = new List<CoreServer>(Plugin.config.GetServers());
                if (servers.Count <= 0)
                {
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

        private void serverSelectionViewController_selectedServer(CoreServer host)
        {
            _modeSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ServerModeSelectionCoordinator>();
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
