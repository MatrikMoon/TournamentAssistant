using CustomUI.BeatSaber;
using SongCore;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class IntroFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator;
        private MatchFlowCoordinator _matchFlowCoordinator;

        private IntroViewController _introViewController;
        private GeneralNavigationController _mainModNavigationController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Tournament Waiting Screen";

                _mainFlowCoordinator = _mainFlowCoordinator ?? Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
                
                _introViewController = BeatSaberUI.CreateViewController<IntroViewController>();

                _mainModNavigationController = BeatSaberUI.CreateViewController<GeneralNavigationController>();
                _mainModNavigationController.didFinishEvent += (_) => _mainFlowCoordinator.InvokeMethod("DismissFlowCoordinator", this, null, false);

                ProvideInitialViewControllers(_mainModNavigationController);

                SetViewControllersToNavigationConctroller(_mainModNavigationController, new VRUIViewController[] { _introViewController });

                //Set up Client
                Config.LoadConfig();

                Action<string, ulong> onUsernameResolved = (username, _) =>
                {
                    if (Plugin.client?.Connected == true) Plugin.client.Shutdown();
                    Plugin.client = new Client(Config.HostName, username);
                    Plugin.client.ConnectedToServer += Client_ConnectedToServer;
                    Plugin.client.FailedToConnectToServer += Client_FailedToConnectToServer;
                    Plugin.client.Start();
                };

                PlayerUtils.GetPlatformUserData(onUsernameResolved);
            }
        }

        private void Client_FailedToConnectToServer()
        {
            _introViewController.SetStatusText("Failed to connect to Host Server on initial attempt... Retrying...");
        }

        private void Client_ConnectedToServer()
        {
            _introViewController.SetStatusText("Connected to Host Server!\n(You may safely back out of this screen to exit the Tournament)");
            if (_matchFlowCoordinator == null)
            {
                _matchFlowCoordinator = gameObject.AddComponent<MatchFlowCoordinator>();
                _matchFlowCoordinator.DidFinishEvent += () => DismissFlowCoordinator(_matchFlowCoordinator);
            }
            PresentFlowCoordinator(_matchFlowCoordinator);
        }

        public void PresentUI()
        {
            _mainFlowCoordinator = _mainFlowCoordinator ?? Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            _mainFlowCoordinator.InvokeMethod("PresentFlowCoordinatorOrAskForTutorial", this);
        }
    }
}
