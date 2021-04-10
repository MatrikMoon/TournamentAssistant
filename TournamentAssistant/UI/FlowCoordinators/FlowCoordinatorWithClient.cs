using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithClient : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;
        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();

        protected bool ShouldDismissOnReturnToMenu { get; set; }

        public CoreServer Host { get; set; }

        private bool _didAttemptConnectionYet;
        private bool _didCreateClient;
        private OngoingGameList _ongoingGameList;
        private GameplaySetupViewController _gameplaySetupViewController;

        protected virtual void OnUserDataResolved(string username, ulong userId)
        {
            if (Plugin.client == null || Plugin.client?.Connected == false)
            {
                Plugin.client = new PluginClient(Host.Address, Host.Port, username, userId.ToString());
                _didCreateClient = true;
            }
            Plugin.client.ConnectedToServer += Client_ConnectedToServer;
            Plugin.client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Plugin.client.ServerDisconnected += Client_ServerDisconnected;
            Plugin.client.PlayerInfoUpdated += Client_PlayerInfoUpdated;
            Plugin.client.LoadedSong += Client_LoadedSong;
            Plugin.client.PlaySong += Client_PlaySong;
            Plugin.client.MatchCreated += Client_MatchCreated;
            Plugin.client.MatchInfoUpdated += Client_MatchInfoUpdated;
            Plugin.client.MatchDeleted += Client_MatchDeleted;
            if (Plugin.client?.Connected == false) Plugin.client.Start();
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                _didAttemptConnectionYet = false;

                _ongoingGameList = BeatSaberUI.CreateViewController<OngoingGameList>();
                _gameplaySetupViewController = Resources.FindObjectsOfTypeAll<GameplaySetupViewController>().First();
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                Plugin.client.ConnectedToServer -= Client_ConnectedToServer;
                Plugin.client.FailedToConnectToServer -= Client_FailedToConnectToServer;
                Plugin.client.ServerDisconnected -= Client_ServerDisconnected;
                Plugin.client.PlayerInfoUpdated -= Client_PlayerInfoUpdated;
                Plugin.client.LoadedSong -= Client_LoadedSong;
                Plugin.client.PlaySong -= Client_PlaySong;
                Plugin.client.MatchCreated -= Client_MatchCreated;
                Plugin.client.MatchInfoUpdated -= Client_MatchInfoUpdated;
                Plugin.client.MatchDeleted -= Client_MatchDeleted;

                if (_didCreateClient) Plugin.client.Shutdown();
            }
        }

        protected override void TransitionDidFinish()
        {
            base.TransitionDidFinish();

            if (!_didAttemptConnectionYet)
            {
                _didAttemptConnectionYet = true;
                PlayerUtils.GetPlatformUserData(OnUserDataResolved);
            }
        }

        public virtual void Dismiss()
        {
            if (_ongoingGameList.isInViewControllerHierarchy)
            {
                SetLeftScreenViewController(null, ViewController.AnimationType.None);
                SetRightScreenViewController(null, ViewController.AnimationType.None);
            }
            RaiseDidFinishEvent();
        }

        //This is here just in case the user quits the game after having connected to the server
        public void OnApplicationQuit()
        {
            Plugin.client.Shutdown();
        }

        protected virtual void Client_ConnectedToServer(ConnectResponse response)
        {
            //In case this coordiator is reused, re-set the dismiss-on-disconnect flag
            ShouldDismissOnReturnToMenu = false;

            //When we're connected to the server, we should update our self to show our mod list
            (Plugin.client.Self as Player).ModList = IPA.Loader.PluginManager.EnabledPlugins.Select(x => x.Id).ToArray();
            Plugin.client.UpdatePlayer(Plugin.client.Self as Player);

            //Needs to run on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _gameplaySetupViewController.Setup(false, true, true, GameplaySetupViewController.GameplayMode.SinglePlayer);
                SetLeftScreenViewController(_gameplaySetupViewController, ViewController.AnimationType.In);
                SetRightScreenViewController(_ongoingGameList, ViewController.AnimationType.In);
                _ongoingGameList.ClearMatches();
                _ongoingGameList.AddMatches(Plugin.client.State.Matches);
            });
        }

        protected virtual void Client_FailedToConnectToServer(ConnectResponse response)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                SetLeftScreenViewController(null, ViewController.AnimationType.None);
                SetRightScreenViewController(null, ViewController.AnimationType.None);
            });
        }

        protected virtual void Client_ServerDisconnected()
        {
            //There's no recourse but to boot the client out if the server disconnects
            //Only the coordinator that created the client should do this, it can handle
            //dismissing any of its children as well
            if (_didCreateClient && Plugin.IsInMenu()) UnityMainThreadDispatcher.Instance().Enqueue(Dismiss);

            //If we're not currently in the menu and/or we're not the parent FlowCoordinatorWithClient,
            //we can use this to know that we should dismiss ourself when we get back from the game scene
            ShouldDismissOnReturnToMenu = true;
        }

        protected virtual void Client_PlayerInfoUpdated(Player player) { }

        protected virtual void Client_LoadedSong(IBeatmapLevel level) { }

        protected virtual void Client_PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers gameOptions, PlayerSpecificSettings playerOptions, OverrideEnvironmentSettings environmentSettings, ColorScheme colors, bool floatingScoreboard, bool streamSync, bool disableFail, bool disablePause) { }

        protected virtual void Client_MatchCreated(Match match)
        {
            _ongoingGameList.AddMatch(match);
        }

        protected virtual void Client_MatchInfoUpdated(Match match) { }

        protected virtual void Client_MatchDeleted(Match match)
        {
            _ongoingGameList.RemoveMatch(match);
        }
    }
}
