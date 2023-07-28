using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Interop;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using UnityEngine.UI;
using Response = TournamentAssistantShared.Models.Packets.Response;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithClient : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();

        protected bool ShouldDismissOnReturnToMenu { get; set; }

        public CoreServer Server { get; set; }
        public string TournamentId { get; set; }


        private bool _didAttemptConnectionYet;
        private bool _didAttemptJoinWithPasswordYet;
        private bool _didCreateClient;
        private string _enteredPassword;

        private OngoingGameList _ongoingGameList;
        private PasswordEntry _passwordEntry;
        private GameplaySetupViewController _gameplaySetupViewController;

        protected void SetBackButtonVisibility(bool enable)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                showBackButton = enable;

                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.SetBackButton(enable, false);
            });
        }

        protected void SetBackButtonInteractivity(bool enable)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.GetField<Button>("_backButton").interactable = enable;
            });
        }

        protected virtual async Task OnUserDataResolved_ActivateClient(string username, ulong userId)
        {
            await ActivateClient(username, userId.ToString());
        }

        protected virtual async Task OnUserDataResolved_JoinTournament(string username, ulong userId)
        {
            await Plugin.client.JoinTournament(TournamentId, username, userId.ToString(), _enteredPassword);
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                _didAttemptConnectionYet = false;
                _didAttemptJoinWithPasswordYet = false;
                _enteredPassword = string.Empty;

                _ongoingGameList = BeatSaberUI.CreateViewController<OngoingGameList>();
                _passwordEntry = BeatSaberUI.CreateViewController<PasswordEntry>();

                _passwordEntry.PasswordEntered += PasswordEntry_PasswordEntered;

                _gameplaySetupViewController = Resources.FindObjectsOfTypeAll<GameplaySetupViewController>().First();
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                DeactivateClient();
            }
        }

        protected override void TransitionDidFinish()
        {
            base.TransitionDidFinish();

            if (!_didAttemptConnectionYet)
            {
                _didAttemptConnectionYet = true;

                SetBackButtonVisibility(true);
                SetBackButtonInteractivity(false);

                //TODO: Review whether this could cause issues. Probably need debouncing or something similar
                Task.Run(() => PlayerUtils.GetPlatformUserData(OnUserDataResolved_ActivateClient));
            }
        }

        private async Task Client_ConnectedToServer(Response.Connect response)
        {
            await PlayerUtils.GetPlatformUserData(OnUserDataResolved_JoinTournament);
            await ConnectedToServer(response);
        }

        private async Task Client_FailedToConnectToServer(Response.Connect response)
        {
            SetBackButtonInteractivity(true);
            await FailedToConnectToServer(response);
        }

        private Task Client_JoinedTournament(Response.Join response)
        {
            //Dismiss the passwordEntry controller before moving on
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                SetBackButtonInteractivity(true);

                if (topViewController is PasswordEntry)
                {
                    DismissViewController(_passwordEntry, immediately: true);
                }
            });

            return JoinedTournament(response);
        }

        private Task Client_FailedToJoinTournament(Response.Join response)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                SetBackButtonInteractivity(true);

                SetLeftScreenViewController(null, ViewController.AnimationType.None);
                SetRightScreenViewController(null, ViewController.AnimationType.None);

                if (response?.Reason == Response.Join.JoinFailReason.IncorrectPassword && !_didAttemptJoinWithPasswordYet)
                {
                    PresentViewController(_passwordEntry, immediately: true);
                }
                else if (topViewController is PasswordEntry)
                {
                    //If we've already attempted to join with password, and fail, then we should try to dismiss the password entry screen
                    DismissViewController(_passwordEntry, immediately: true);
                }
            });

            if (response?.Reason != Response.Join.JoinFailReason.IncorrectPassword || _didAttemptJoinWithPasswordYet)
            {
                return FailedToJoinTournament(response);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        private void PasswordEntry_PasswordEntered(string password)
        {
            _enteredPassword = password;

            //Try to start the client again
            //TODO: Review whether this could cause issues. Probably need debouncing or something similar
            _didAttemptJoinWithPasswordYet = true;
            Task.Run(() => PlayerUtils.GetPlatformUserData(OnUserDataResolved_JoinTournament));
        }

        private async Task ActivateClient(string username, string userId)
        {
            if (Plugin.client == null || Plugin.client?.Connected == false)
            {
                var modList = IPA.Loader.PluginManager.EnabledPlugins.Select(x => x.Id).ToList();
                Plugin.client = new PluginClient(Server.Address, Server.Port);
                Plugin.client.SetAuthToken(TAAuthLibraryWrapper.GetToken(username, userId));
                _didCreateClient = true;
            }
            Plugin.client.ConnectedToServer += Client_ConnectedToServer;
            Plugin.client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Plugin.client.JoinedTournament += Client_JoinedTournament;
            Plugin.client.FailedToJoinTournament += Client_FailedToJoinTournament;
            Plugin.client.ServerDisconnected += ServerDisconnected;
            Plugin.client.LoadedSong += LoadedSong;
            Plugin.client.PlaySong += PlaySong;
            Plugin.client.StateManager.UserInfoUpdated += UserInfoUpdated;
            Plugin.client.StateManager.MatchCreated += MatchCreated;
            Plugin.client.StateManager.MatchInfoUpdated += MatchInfoUpdated;
            Plugin.client.StateManager.MatchDeleted += MatchDeleted;
            Plugin.client.ShowModal += ShowModal;
            if (Plugin.client?.Connected == false) await Plugin.client.Start();
        }

        private void DeactivateClient()
        {
            Plugin.client.ConnectedToServer -= ConnectedToServer;
            Plugin.client.FailedToConnectToServer -= FailedToConnectToServer;
            Plugin.client.JoinedTournament -= Client_JoinedTournament;
            Plugin.client.FailedToJoinTournament -= Client_FailedToJoinTournament;
            Plugin.client.ServerDisconnected -= ServerDisconnected;
            Plugin.client.LoadedSong -= LoadedSong;
            Plugin.client.PlaySong -= PlaySong;
            Plugin.client.StateManager.UserInfoUpdated -= UserInfoUpdated;
            Plugin.client.StateManager.MatchCreated -= MatchCreated;
            Plugin.client.StateManager.MatchInfoUpdated -= MatchInfoUpdated;
            Plugin.client.StateManager.MatchDeleted -= MatchDeleted;
            Plugin.client.ShowModal -= ShowModal;

            if (_didCreateClient) Plugin.client.Shutdown();
        }

        public virtual void Dismiss()
        {
            if (topViewController is PasswordEntry)
            {
                DismissViewController(_passwordEntry, immediately: true);
            }
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

        protected virtual Task ConnectedToServer(Response.Connect response)
        {
            //In case this coordiator is reused, re-set the dismiss-on-disconnect flag
            ShouldDismissOnReturnToMenu = false;

            return Task.CompletedTask;
        }

        protected virtual Task FailedToConnectToServer(Response.Connect response) { return Task.CompletedTask; }

        protected virtual Task JoinedTournament(Response.Join response)
        {
            TournamentId = response.TournamentId;
            Plugin.client.SelectedTournament = response.TournamentId;

            //Needs to run on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _gameplaySetupViewController.Setup(false, true, true, false, PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);
                SetLeftScreenViewController(_gameplaySetupViewController, ViewController.AnimationType.In);
                SetRightScreenViewController(_ongoingGameList, ViewController.AnimationType.In);
                _ongoingGameList.SetMatches(Plugin.client.StateManager.GetMatches(TournamentId).ToArray());
            });
            return Task.CompletedTask;
        }

        protected virtual Task FailedToJoinTournament(Response.Join response) { return Task.CompletedTask; }

        protected virtual Task ServerDisconnected()
        {
            //There's no recourse but to boot the client out if the server disconnects
            //Only the coordinator that created the client should do this, it can handle
            //dismissing any of its children as well
            if (_didCreateClient && Plugin.IsInMenu()) UnityMainThreadDispatcher.Instance().Enqueue(Dismiss);

            //If we're not currently in the menu and/or we're not the parent FlowCoordinatorWithClient,
            //we can use this to know that we should dismiss ourself when we get back from the game scene
            ShouldDismissOnReturnToMenu = true;

            return Task.CompletedTask;
        }

        protected virtual Task UserInfoUpdated(User user) { return Task.CompletedTask; }

        protected virtual Task LoadedSong(IBeatmapLevel level) { return Task.CompletedTask; }

        protected virtual Task PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers gameOptions, PlayerSpecificSettings playerOptions, OverrideEnvironmentSettings environmentSettings, ColorScheme colors, bool floatingScoreboard, bool streamSync, bool disableFail, bool disablePause) { return Task.CompletedTask; }

        protected virtual Task MatchCreated(Match match)
        {
            _ongoingGameList.SetMatches(Plugin.client.StateManager.GetMatches(TournamentId).ToArray());
            return Task.CompletedTask;
        }

        protected virtual Task MatchInfoUpdated(Match match) { return Task.CompletedTask; }

        protected virtual Task MatchDeleted(Match match)
        {
            _ongoingGameList.SetMatches(Plugin.client.StateManager.GetMatches(TournamentId).ToArray());
            return Task.CompletedTask;
        }

        protected virtual Task ShowModal(Request.ShowModal message) { return Task.CompletedTask; }
    }
}
