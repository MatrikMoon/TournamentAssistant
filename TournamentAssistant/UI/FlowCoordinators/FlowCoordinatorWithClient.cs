using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities.Async;
using System;
using System.Linq;
using System.Threading.Tasks;
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
        private bool _didAttemptConnectionWithPasswordYet;
        private bool _didCreateClient;
        private string _enteredPassword;

        private OngoingGameList _ongoingGameList;
        private PasswordEntry _passwordEntry;
        private GameplaySetupViewController _gameplaySetupViewController;
        private GameplayModifiersPanelController _gameplayModifiersPanelController;

        protected virtual async Task OnUserDataResolved(string username, ulong userId)
        {
            await ActivateClient(username, userId);
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                _didAttemptConnectionYet = false;
                _didAttemptConnectionWithPasswordYet = false;
                _enteredPassword = string.Empty;

                _ongoingGameList = BeatSaberUI.CreateViewController<OngoingGameList>();
                _passwordEntry = BeatSaberUI.CreateViewController<PasswordEntry>();

                _passwordEntry.PasswordEntered += PasswordEntry_PasswordEntered;

                _gameplaySetupViewController = Resources.FindObjectsOfTypeAll<GameplaySetupViewController>().First();
                _gameplayModifiersPanelController = Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().First();
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

                //TODO: Review whether this could cause issues. Probably need debouncing or something similar
                Task.Run(() => PlayerUtils.GetPlatformUserData(OnUserDataResolved));
            }
        }

        private Task Client_ConnectedToServer(Response.Connect response)
        {
            //Dismiss the passwordEntry controller before moving on
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                if (topViewController is PasswordEntry)
                {
                    DismissViewController(_passwordEntry, immediately: true);
                }
            });

            return ConnectedToServer(response);
        }

        private Task Client_FailedToConnectToServer(Response.Connect response)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                SetLeftScreenViewController(null, ViewController.AnimationType.None);
                SetRightScreenViewController(null, ViewController.AnimationType.None);

                if (response?.Reason == Response.ConnectFailReason.IncorrectPassword && !_didAttemptConnectionWithPasswordYet)
                {
                    PresentViewController(_passwordEntry, immediately: true);
                }
                else if (topViewController is PasswordEntry)
                {
                    //If we've already attempted to connect, and fail, then we should try to dismiss the password entry screen
                    DismissViewController(_passwordEntry, immediately: true);
                }
            });

            if (response?.Reason != Response.ConnectFailReason.IncorrectPassword || _didAttemptConnectionWithPasswordYet)
            {
                return FailedToConnectToServer(response);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        private void PasswordEntry_PasswordEntered(string password)
        {
            _enteredPassword = password;

            //Deactivate and reset client
            DeactivateClient();
            Plugin.client = null;

            //Try to start the client again
            //TODO: Review whether this could cause issues. Probably need debouncing or something similar
            _didAttemptConnectionWithPasswordYet = true;
            Task.Run(() => PlayerUtils.GetPlatformUserData(OnUserDataResolved));
        }

        private async Task ActivateClient(string username, ulong userId)
        {
            if (Plugin.client == null || Plugin.client?.Connected == false)
            {
                var modList = IPA.Loader.PluginManager.EnabledPlugins.Select(x => x.Id).ToList();
                Plugin.client = new PluginClient(Host.Address, Host.Port, username, userId.ToString(), _enteredPassword, modList: modList);
                _didCreateClient = true;
            }
            Plugin.client.ConnectedToServer += Client_ConnectedToServer;
            Plugin.client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Plugin.client.ServerDisconnected += ServerDisconnected;
            Plugin.client.UserInfoUpdated += UserInfoUpdated;
            Plugin.client.LoadedSong += LoadedSong;
            Plugin.client.PlaySong += PlaySong;
            Plugin.client.MatchCreated += MatchCreated;
            Plugin.client.MatchInfoUpdated += MatchInfoUpdated;
            Plugin.client.MatchDeleted += MatchDeleted;
            Plugin.client.ShowModal += ShowModal;
            if (Plugin.client?.Connected == false) await Plugin.client.Start();
        }

        private void DeactivateClient()
        {
            Plugin.client.ConnectedToServer -= ConnectedToServer;
            Plugin.client.FailedToConnectToServer -= FailedToConnectToServer;
            Plugin.client.ServerDisconnected -= ServerDisconnected;
            Plugin.client.UserInfoUpdated -= UserInfoUpdated;
            Plugin.client.LoadedSong -= LoadedSong;
            Plugin.client.PlaySong -= PlaySong;
            Plugin.client.MatchCreated -= MatchCreated;
            Plugin.client.MatchInfoUpdated -= MatchInfoUpdated;
            Plugin.client.MatchDeleted -= MatchDeleted;
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

            ReenableDisallowedModifierToggles(_gameplayModifiersPanelController);

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

            //Needs to run on main thread
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _gameplaySetupViewController.Setup(true, true, true, false, PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);
                SetLeftScreenViewController(_gameplaySetupViewController, ViewController.AnimationType.In);

                DisableDisallowedModifierToggles(_gameplayModifiersPanelController);

                SetRightScreenViewController(_ongoingGameList, ViewController.AnimationType.In);
                _ongoingGameList.SetMatches(Plugin.client.State.Matches.ToArray());
            });
            return Task.CompletedTask;
        }

        private void DisableDisallowedModifierToggles(GameplayModifiersPanelController controller)
        {
            var toggles = controller.GetField<GameplayModifierToggle[]>("_gameplayModifierToggles");
            var disallowedToggles = toggles.Where(x => x.name != "ProMode");

            foreach (var toggle in disallowedToggles)
            {
                toggle.gameObject.SetActive(false);
            }
        }

        private void ReenableDisallowedModifierToggles(GameplayModifiersPanelController controller)
        {
            var toggles = controller.GetField<GameplayModifierToggle[]>("_gameplayModifierToggles");

            if (toggles != null)
            {
                foreach (var toggle in toggles)
                {
                    toggle.gameObject.SetActive(true);
                }
            }
        }

        protected virtual Task FailedToConnectToServer(Response.Connect response) { return Task.CompletedTask; }

        protected virtual Task ServerDisconnected()
        {
            //There's no recourse but to boot the client out if the server disconnects
            //Only the coordinator that created the client should do this, it can handle
            //dismissing any of its children as well
            if (_didCreateClient && Plugin.IsInMenu()) UnityMainThreadTaskScheduler.Factory.StartNew(Dismiss);

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
            _ongoingGameList.SetMatches(Plugin.client.State.Matches.ToArray());
            return Task.CompletedTask;
        }

        protected virtual Task MatchInfoUpdated(Match match) { return Task.CompletedTask; }

        protected virtual Task MatchDeleted(Match match)
        {
            _ongoingGameList.SetMatches(Plugin.client.State.Matches.ToArray());
            return Task.CompletedTask;
        }

        protected virtual Task ShowModal(Command.ShowModal message) { return Task.CompletedTask; }
    }
}
