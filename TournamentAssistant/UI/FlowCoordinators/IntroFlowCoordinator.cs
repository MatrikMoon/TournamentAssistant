using CustomUI.BeatSaber;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.UI.NavigationControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using UnityEngine;
using VRUI;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class IntroFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator;
        private MatchFlowCoordinator _matchFlowCoordinator;

        private IntroViewController _introViewController;
        private MatchListViewController _matchListViewController;
        private GeneralNavigationController _mainModNavigationController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Tournament Waiting Screen";
                
                _introViewController = _introViewController ?? BeatSaberUI.CreateViewController<IntroViewController>();
                _matchListViewController = _matchListViewController ?? BeatSaberUI.CreateViewController<MatchListViewController>();

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
                    Plugin.client.StateUpdated += Client_StateUpdated;
                    Plugin.client.MatchCreated += Client_MatchCreated;
                    Plugin.client.MatchDeleted += Client_MatchDeleted;
                    Plugin.client.Start();
                };

                PlayerUtils.GetPlatformUserData(onUsernameResolved);
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy) Plugin.client.Shutdown();
        }

        //This is here just in case the user quits the game after having connected to the server
        public void OnApplicationQuit()
        {
            Plugin.client.Shutdown();
        }

        private void Client_StateUpdated(TournamentState state)
        {
            _matchListViewController.ClearMatches();
            _matchListViewController.AddMatches(state.Matches);
        }

        private void Client_MatchDeleted(Match match)
        {
            _matchListViewController.RemoveMatch(match);
        }

        private void Client_MatchCreated(Match match)
        {
            _matchListViewController.AddMatch(match);

            if (match.Players.Contains(Plugin.client.Self))
            {
                if (_matchFlowCoordinator == null)
                {
                    _matchFlowCoordinator = gameObject.AddComponent<MatchFlowCoordinator>();
                    _matchFlowCoordinator.DidFinishEvent += () => DismissFlowCoordinator(_matchFlowCoordinator);
                }

                _matchFlowCoordinator.Match = match;

                //Needs to run on main thread
                UnityMainThreadDispatcher.Instance().Enqueue(() => PresentFlowCoordinator(_matchFlowCoordinator));
            }
        }

        private void Client_FailedToConnectToServer()
        {
            _introViewController.StatusText = "Failed to connect to Host Server on initial attempt... Retrying...";
        }

        private void Client_ConnectedToServer()
        {
            _introViewController.StatusText = "Connected to Host Server!\n(You may safely back out of this screen to exit the Tournament)";

            //Needs to run on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => SetLeftScreenViewController(_matchListViewController));
        }

        public void PresentUI()
        {
            _mainFlowCoordinator = _mainFlowCoordinator ?? Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            _mainFlowCoordinator.InvokeMethod("PresentFlowCoordinatorOrAskForTutorial", this);
        }
    }
}
