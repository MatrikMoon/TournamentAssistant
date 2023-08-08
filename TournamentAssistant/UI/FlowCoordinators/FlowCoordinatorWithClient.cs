using HMUI;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Interop;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine.UI;
using Response = TournamentAssistantShared.Models.Packets.Response;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithClient : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();

        protected bool ShouldDismissOnReturnToMenu { get; set; }

        private bool _didAttemptConnectionYet;
        private bool _didCreateClient;

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

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                _didAttemptConnectionYet = false;
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
            SetBackButtonInteractivity(true);
            await ConnectedToServer(response);
        }

        private async Task Client_FailedToConnectToServer(Response.Connect response)
        {
            SetBackButtonInteractivity(true);
            await FailedToConnectToServer(response);
        }

        private async Task ActivateClient(string username, string userId)
        {
            if (Plugin.client == null || Plugin.client?.Connected == false)
            {
                var modList = IPA.Loader.PluginManager.EnabledPlugins.Select(x => x.Id).ToList();
                Plugin.client = new PluginClient(TournamentServer.Address, TournamentServer.Port);
                Plugin.client.SetAuthToken(TAAuthLibraryWrapper.GetToken(username, userId));
                _didCreateClient = true;
            }
            Plugin.client.ConnectedToServer += Client_ConnectedToServer;
            Plugin.client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Plugin.client.ServerDisconnected += ServerDisconnected;
            Plugin.client.LoadedSong += LoadedSong;
            Plugin.client.PlaySong += PlaySong;
            Plugin.client.StateManager.UserInfoUpdated += UserUpdated;
            Plugin.client.StateManager.MatchCreated += MatchCreated;
            Plugin.client.StateManager.MatchInfoUpdated += MatchUpdated;
            Plugin.client.StateManager.MatchDeleted += MatchDeleted;
            Plugin.client.ShowModal += ShowModal;
            if (Plugin.client?.Connected == false) await Plugin.client.Start();
        }

        private void DeactivateClient()
        {
            Plugin.client.ConnectedToServer -= ConnectedToServer;
            Plugin.client.FailedToConnectToServer -= FailedToConnectToServer;
            Plugin.client.ServerDisconnected -= ServerDisconnected;
            Plugin.client.LoadedSong -= LoadedSong;
            Plugin.client.PlaySong -= PlaySong;
            Plugin.client.StateManager.UserInfoUpdated -= UserUpdated;
            Plugin.client.StateManager.MatchCreated -= MatchCreated;
            Plugin.client.StateManager.MatchInfoUpdated -= MatchUpdated;
            Plugin.client.StateManager.MatchDeleted -= MatchDeleted;
            Plugin.client.ShowModal -= ShowModal;

            if (_didCreateClient) Plugin.client.Shutdown();
        }

        public virtual void Dismiss()
        {
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
            Plugin.client.SelectedTournament = response.TournamentId;

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

        protected virtual Task LoadedSong(IBeatmapLevel level) { return Task.CompletedTask; }

        protected virtual Task PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers gameOptions, PlayerSpecificSettings playerOptions, OverrideEnvironmentSettings environmentSettings, ColorScheme colors, bool floatingScoreboard, bool streamSync, bool disableFail, bool disablePause) { return Task.CompletedTask; }

        protected virtual Task TournamentCreated(Tournament tournament) { return Task.CompletedTask; }

        protected virtual Task TournamentUpdated(Tournament tournament) { return Task.CompletedTask; }

        protected virtual Task TournamentDeleted(Tournament tournament) { return Task.CompletedTask; }

        protected virtual Task UserUpdated(User user) { return Task.CompletedTask; }

        protected virtual Task MatchCreated(Match match) { return Task.CompletedTask; }

        protected virtual Task MatchUpdated(Match match) { return Task.CompletedTask; }

        protected virtual Task MatchDeleted(Match match) { return Task.CompletedTask; }

        protected virtual Task ShowModal(Request.ShowModal message) { return Task.CompletedTask; }
    }
}
