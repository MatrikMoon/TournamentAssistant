using BS_Utils.Gameplay;
using HMUI;
using IPA.Utilities.Async;
using System;
using System.Threading.Tasks;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Interop;
using TournamentAssistant.UnityUtilities;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Response = TournamentAssistantShared.Models.Packets.Response;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithClient : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        public CoreServer Server { get; set; }

        protected void RaiseDidFinishEvent() => DidFinishEvent?.Invoke();

        protected bool ShouldDismissOnReturnToMenu { get; set; }
        protected PluginClient Client { get; set; }

        private bool _didAttemptConnectionYet;
        private bool _didCreateClient;

        protected void SetBackButtonVisibility(bool enable)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                showBackButton = enable;

                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.SetBackButton(enable, false);
            });
        }

        protected void SetBackButtonInteractivity(bool enable)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.GetField<Button>("_backButton").interactable = enable;
            });
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                _didAttemptConnectionYet = false;
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                DeactivateClient();
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "GameCore")
            {
                if (Client?.Connected ?? false && Client.SelectedTournament != null)
                {
                    // Add the score monitor so coordinators and overlays can see realtime score updates
                    /*if (ScoreMonitor.Instance == null)
                    {
                        new GameObject("ScoreMonitor").AddComponent<ScoreMonitor>();
                    }*/

                    /*if (Plugin.UseFloatingScoreboard && FloatingScoreScreen.Instance == null)
                    {
                        new GameObject("FloatingScoreScreen").AddComponent<FloatingScoreScreen>();
                        Plugin.UseFloatingScoreboard = false;
                    }*/

                    if (Plugin.DisableFail)
                    {
                        new GameObject("AntiFail").AddComponent<AntiFail>();
                        Plugin.DisableFail = false;
                    }

                    if (Plugin.UseSync && SyncHandler.Instance == null)
                    {
                        new GameObject("SyncHandler").AddComponent<SyncHandler>();
                        Plugin.UseSync = false;
                    }

                    if (Plugin.DisablePause)
                    {
                        AntiPause.AllowPause = false;
                        Plugin.DisablePause = false;
                    }

                    if (Plugin.QualifierDisablePause)
                    {
                        AntiPause.AllowContinueAfterPause = false;
                        Plugin.QualifierDisablePause = false;
                    }

                    // Tell the server we're in-game, if applicable
                    if (!string.IsNullOrEmpty(Client.SelectedTournament))
                    {
                        var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());
                        player.PlayState = User.PlayStates.InGame;
                        Task.Run(() => Client.UpdateUser(Client.SelectedTournament, player));
                    }
                }
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "GameCore")
            {
                if (Client?.Connected ?? false && Client.SelectedTournament != null)
                {
                    AntiPause.AllowPause = true;
                    AntiPause.AllowContinueAfterPause = true;

                    /*if (ScoreMonitor.Instance != null)
                    {
                        ScoreMonitor.Destroy();
                    }*/

                    if (SyncHandler.Instance != null)
                    {
                        SyncHandler.Destroy();
                    }

                    /*if (FloatingScoreScreen.Instance != null)
                    {
                        FloatingScoreScreen.Destroy();
                    }*/

                    // Tell the server we're no longer in-game, if applicable
                    if (!string.IsNullOrEmpty(Client.SelectedTournament))
                    {
                        var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());
                        player.PlayState = User.PlayStates.Waiting;
                        Task.Run(() => Client.UpdateUser(Client.SelectedTournament, player));
                    }
                }
                if (ShouldDismissOnReturnToMenu)
                {
                    UnityMainThreadTaskScheduler.Factory.StartNew(Dismiss);
                }
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
                Task.Run(async () =>
                {
                    var user = await GetUserInfo.GetUserAsync();
                    await ActivateClient(user.userName, user.platformUserId);
                });
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
            if (Client == null || Client?.Connected == false)
            {
                Client = new PluginClient(Server.Address, Server.Port);
                Client.SetAuthToken(TAAuthLibraryWrapper.GetToken(username, userId));
                _didCreateClient = true;
            }
            Client.ConnectedToServer += Client_ConnectedToServer;
            Client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Client.ServerDisconnected += ServerDisconnected;

            if (Client?.Connected == false) await Client.Connect();
        }

        private void DeactivateClient()
        {
            Client.ConnectedToServer -= ConnectedToServer;
            Client.FailedToConnectToServer -= FailedToConnectToServer;
            Client.ServerDisconnected -= ServerDisconnected;

            if (_didCreateClient) Client.Shutdown();
        }

        public virtual void Dismiss()
        {
            RaiseDidFinishEvent();
        }

        //This is here just in case the user quits the game after having connected to the server
        public void OnApplicationQuit()
        {
            DeactivateClient();
        }

        protected virtual Task ConnectedToServer(Response.Connect response)
        {
            //In case this coordiator is reused, re-set the dismiss-on-disconnect flag
            ShouldDismissOnReturnToMenu = false;

            return Task.CompletedTask;
        }

        protected virtual Task FailedToConnectToServer(Response.Connect response) { return Task.CompletedTask; }

        protected virtual Task ServerDisconnected()
        {
            //There's no recourse but to boot the client out if the server disconnects
            //Only the coordinator that created the client should do this, it can handle
            //dismissing any of its children as well
            if (_didCreateClient && Plugin.IsInMenu())
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(Dismiss);
            }

            //If we're not currently in the menu and/or we're not the parent FlowCoordinatorWithClient,
            //we can use this to know that we should dismiss ourself when we get back from the game scene
            ShouldDismissOnReturnToMenu = true;

            return Task.CompletedTask;
        }
    }
}
