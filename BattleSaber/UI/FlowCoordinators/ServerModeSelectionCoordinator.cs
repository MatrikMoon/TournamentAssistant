using BattleSaber.Misc;
using BattleSaber.Models;
using BattleSaber.UI.ViewControllers;
using BattleSaber.Utilities;
using BattleSaberShared.Models;
using BattleSaberShared.Models.Packets;
using BeatSaberMarkupLanguage;
using HMUI;
using System;
using Match = BattleSaberShared.Models.Match;

namespace BattleSaber.UI.FlowCoordinators
{
    class ServerModeSelectionCoordinator : FlowCoordinator
    {
        public CoreServer Host { get; set; }

        public event Action DidFinishEvent;

        private TournamentMatchCoordinator _matchFlowCoordinator;
        private RoomSelectionCoordinator _roomSelectionCoordinator;
 
        private ServerModeSelection _serverModeSelectionViewController;
        private OngoingGameList _ongoingGameList;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Choose Your Path";
                showBackButton = true;

                _serverModeSelectionViewController = BeatSaberUI.CreateViewController<ServerModeSelection>();
                _serverModeSelectionViewController.BattleSaberButtonPressed += serverModeSelectionViewController_BattleSaberButtonPressed;
                _serverModeSelectionViewController.TournamentButtonPressed += serverModeSelectionViewController_TournamentButtonPressed;

                _ongoingGameList = BeatSaberUI.CreateViewController<OngoingGameList>();

                ProvideInitialViewControllers(_serverModeSelectionViewController);

                Action<string, ulong> onUsernameResolved = (username, _) =>
                {
                    if (Plugin.client?.Connected == true) Plugin.client.Shutdown();
                    Plugin.client = new PluginClient(Host.Address, Host.Port, username);
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
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                _serverModeSelectionViewController.BattleSaberButtonPressed -= serverModeSelectionViewController_BattleSaberButtonPressed;
                _serverModeSelectionViewController.TournamentButtonPressed -= serverModeSelectionViewController_TournamentButtonPressed;

                Plugin.client.ConnectedToServer -= Client_ConnectedToServer;
                Plugin.client.FailedToConnectToServer -= Client_FailedToConnectToServer;
                Plugin.client.StateUpdated -= Client_StateUpdated;
                Plugin.client.MatchCreated -= Client_MatchCreated;
                Plugin.client.MatchDeleted -= Client_MatchDeleted;
                Plugin.client.Shutdown();
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DidFinishEvent?.Invoke();
        }

        //This is here just in case the user quits the game after having connected to the server
        public void OnApplicationQuit()
        {
            Plugin.client.Shutdown();
        }

        private void serverModeSelectionViewController_BattleSaberButtonPressed()
        {
            _roomSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomSelectionCoordinator>();
            _roomSelectionCoordinator.DidFinishEvent += roomSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_roomSelectionCoordinator);
        }

        private void roomSelectionCoordinator_DidFinishEvent()
        {
            _roomSelectionCoordinator.DidFinishEvent -= roomSelectionCoordinator_DidFinishEvent;

            DismissFlowCoordinator(_roomSelectionCoordinator);
        }

        private void serverModeSelectionViewController_TournamentButtonPressed()
        {
            if (_matchFlowCoordinator == null)
            {
                _matchFlowCoordinator = BeatSaberUI.CreateFlowCoordinator<TournamentMatchCoordinator>();
                _matchFlowCoordinator.DidFinishEvent += () =>
                {
                    DismissFlowCoordinator(_matchFlowCoordinator);

                    //The mode selection page shouldn't exist in tournament mode
                    if (Plugin.client.ServerSettings.tournamentMode) DidFinishEvent?.Invoke();
                };
            }
            PresentFlowCoordinator(_matchFlowCoordinator);
        }

        private void Client_StateUpdated(State state)
        {
            _ongoingGameList.ClearMatches();
            _ongoingGameList.AddMatches(state.Matches);
        }

        private void Client_MatchDeleted(Match match)
        {
            _ongoingGameList.RemoveMatch(match);
        }

        private void Client_MatchCreated(Match match)
        {
            _ongoingGameList.AddMatch(match);
        }

        private void Client_FailedToConnectToServer(ConnectResponse response)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (response != null && !string.IsNullOrEmpty(response.message)) _serverModeSelectionViewController.StatusText = response.message;
                else _serverModeSelectionViewController.StatusText = "Failed to connect to Host Server on initial attempt... Retrying...";

                _serverModeSelectionViewController.DisableButtons();

                SetLeftScreenViewController(null);
            });
        }

        private void Client_ConnectedToServer(ConnectResponse response)
        {
            //Needs to run on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (!string.IsNullOrEmpty(response.message)) _serverModeSelectionViewController.StatusText = response.message;
                else _serverModeSelectionViewController.StatusText = $"Connected to {Host.Name}!";
                _serverModeSelectionViewController.EnableButtons();

                SetLeftScreenViewController(_ongoingGameList);

                //If Tournament Mode is on, go directly to the tournament page
                if (Plugin.client.ServerSettings.tournamentMode)
                {
                    serverModeSelectionViewController_TournamentButtonPressed();
                }
            });
        }
    }
}
