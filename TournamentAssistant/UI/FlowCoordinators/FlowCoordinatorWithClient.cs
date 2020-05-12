using BeatSaberMarkupLanguage;
using System;
using TournamentAssistant.Misc;
using TournamentAssistant.Models;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FlowCoordinatorWithClient : FinishableFlowCoordinator
    {
        public CoreServer Host { get; set; }

        private OngoingGameList _ongoingGameList;

        protected virtual void OnUserDataResolved(string username, ulong userId)
        {
            if (Plugin.client == null || Plugin.client?.Connected == false) Plugin.client = new PluginClient(Host.Address, Host.Port, username, userId);
            Plugin.client.ConnectedToServer += Client_ConnectedToServer;
            Plugin.client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Plugin.client.StateUpdated += Client_StateUpdated;
            Plugin.client.PlayerInfoUpdated += Client_PlayerInfoUpdated;
            Plugin.client.LoadedSong += Client_LoadedSong;
            Plugin.client.PlaySong += Client_PlaySong;
            Plugin.client.MatchCreated += Client_MatchCreated;
            Plugin.client.MatchInfoUpdated += Client_MatchInfoUpdated;
            Plugin.client.MatchDeleted += Client_MatchDeleted;
            if (Plugin.client?.Connected == false) Plugin.client.Start();
        }

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                _ongoingGameList = BeatSaberUI.CreateViewController<OngoingGameList>();

                PlayerUtils.GetPlatformUserData(OnUserDataResolved);
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                Plugin.client.ConnectedToServer -= Client_ConnectedToServer;
                Plugin.client.FailedToConnectToServer -= Client_FailedToConnectToServer;
                Plugin.client.StateUpdated -= Client_StateUpdated;
                Plugin.client.PlayerInfoUpdated -= Client_PlayerInfoUpdated;
                Plugin.client.LoadedSong -= Client_LoadedSong;
                Plugin.client.PlaySong -= Client_PlaySong;
                Plugin.client.MatchCreated -= Client_MatchCreated;
                Plugin.client.MatchInfoUpdated -= Client_MatchInfoUpdated;
                Plugin.client.MatchDeleted -= Client_MatchDeleted;
            }
        }

        //This is here just in case the user quits the game after having connected to the server
        public void OnApplicationQuit()
        {
            Plugin.client.Shutdown();
        }

        protected virtual void Client_ConnectedToServer(ConnectResponse response)
        {
            //Needs to run on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                SetLeftScreenViewController(_ongoingGameList);
            });
        }

        protected virtual void Client_FailedToConnectToServer(ConnectResponse response)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                SetLeftScreenViewController(null);
            });
        }

        protected virtual void Client_StateUpdated(State state)
        {
            _ongoingGameList.ClearMatches();
            _ongoingGameList.AddMatches(state.Matches);
        }

        protected virtual void Client_PlayerInfoUpdated(Player player) { }

        protected virtual void Client_LoadedSong(IBeatmapLevel level) { }

        protected virtual void Client_PlaySong(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers gameOptions, PlayerSpecificSettings playerOptions, OverrideEnvironmentSettings environmentSettings, ColorScheme colors, bool floatingScoreboard, bool streamSync) { }

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
