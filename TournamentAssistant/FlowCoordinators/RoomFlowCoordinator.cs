using HMUI;
using IPA.Loader;
using IPA.Utilities;
using IPA.Utilities.Async;
using SiraUtil.Tools;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Models;
using TournamentAssistant.ViewControllers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using Zenject;

namespace TournamentAssistant.FlowCoordinators
{
    internal abstract class RoomFlowCoordinator : FlowCoordinator
    {
        protected CoreServer? _host;
        public event Action? DismissRequested;
        protected MenuLightsPresetSO? _defaultLightsPreset;
        protected MenuLightsPresetSO? _resultsClearedLightsPreset;

        [Inject]
        protected readonly SiraLog _siraLog;

        [Inject]
        protected readonly PluginClient _pluginClient = null!;

        [Inject]
        protected readonly PlayerDataModel _playerDataModel = null!;

        [Inject]
        protected readonly MenuLightsManager _menuLightsManager = null!;

        [Inject]
        private readonly OngoingGameListView _ongoingGameListView = null!;

        [Inject]
        protected readonly MenuTransitionsHelper _menuTransitionsHelper = null!;

        [Inject]
        protected readonly ResultsViewController _resultsViewController = null!;

        [Inject]
        private readonly GameplaySetupViewController _gameplaySetupViewController = null!;

        [Inject]
        private readonly SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator = null!;

        protected void Start()
        {
            _defaultLightsPreset = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO, SoloFreePlayFlowCoordinator>("_defaultLightsPreset");
            _resultsClearedLightsPreset = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO, SoloFreePlayFlowCoordinator>("_resultsClearedLightsPreset");
        }

        public void SetHost(CoreServer host)
        {
            _host = host;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (_host is null)
            {
                throw new Exception($"The {nameof(TournamentRoomFlowCoodinator)} does not have a host associated. Make sure to call roomFlowCoordinator.SetHost() when you present it.");
            }
            if (firstActivation)
            {
                showBackButton = true;
            }
            if (addedToHierarchy)
            {
                _ = ActivateAsync();
            }

            if (addedToHierarchy || screenSystemEnabling)
            {
                _pluginClient.ConnectedToServer += PluginClient_ConnectedToServer;
                _pluginClient.MatchCreated += PluginClient_MatchCreated;
                _pluginClient.MatchDeleted += PluginClient_MatchDeleted;
                _pluginClient.PlayerInfoUpdated += PluginClient_PlayerInfoUpdated;
                _pluginClient.FailedToConnectToServer += PluginClient_FailedToConnectToServer;
                _pluginClient.StartLevel += PluginClient_StartLevel;
                _pluginClient.LoadedSong += PluginClient_LoadedSong;
                _pluginClient.ServerDisconnected += PluginClient_ServerDisconnected;
                _pluginClient.MatchInfoUpdated += PluginClient_MatchInfoUpdated;
            }
        }

        private void PluginClient_ServerDisconnected()
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                ServerDisconnected(_pluginClient);
            });
        }

        private void PluginClient_LoadedSong(IBeatmapLevel level)
        {
            SongLoaded(_pluginClient, level);
        }

        private void PluginClient_StartLevel(StartLevelOptions level, MatchOptions match)
        {
            PlaySong(_pluginClient, level, match);
        }

        private void PluginClient_MatchCreated(Match match)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _ongoingGameListView.AddMatches(match);
                MatchCreated(_pluginClient, match);
            });
        }

        private void PluginClient_MatchInfoUpdated(Match match)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                MatchUpdated(_pluginClient, match);
            });
        }

        private void PluginClient_MatchDeleted(Match match)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _ongoingGameListView.RemoveMatch(match);
                MatchDeleted(_pluginClient, match);
            });
        }

        private void PluginClient_PlayerInfoUpdated(Player player)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                PlayerUpdated(_pluginClient, player);
            });
        }

        private void PluginClient_FailedToConnectToServer(ConnectResponse connect)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                SetLeftScreenViewController(null, ViewController.AnimationType.None);
                SetRightScreenViewController(null, ViewController.AnimationType.None);
                FailedToConnect(_pluginClient, connect);
            });
        }

        private void PluginClient_ConnectedToServer(ConnectResponse connect)
        {
            if (_pluginClient.Self is Player player)
            {
                _pluginClient.UpdatePlayer(player);
                player.ModList = PluginManager.EnabledPlugins.Select(x => x.Id).ToArray();
                UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    _gameplaySetupViewController.Setup(false, true, true, GameplaySetupViewController.GameplayMode.SinglePlayer);
                    SetLeftScreenViewController(_gameplaySetupViewController, ViewController.AnimationType.In);
                    SetRightScreenViewController(_ongoingGameListView, ViewController.AnimationType.In);
                    _ongoingGameListView.ClearMatches();
                    _ongoingGameListView.AddMatches(_pluginClient.State.Matches);
                    Connected(_pluginClient, player, connect);
                });
            }
        }

        private async Task ActivateAsync()
        {
            if (_host is null)
            {
                throw new Exception($"The {nameof(TournamentRoomFlowCoodinator)} does not have a host associated. Make sure to call roomFlowCoordinator.SetHost() when you present it.");
            }

            try
            {
                await _pluginClient.Login(_host.Address, _host.Port);
            }
            catch (Exception e)
            {
                _siraLog.Error(e);
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            _pluginClient.Logout();

            if (removedFromHierarchy || screenSystemDisabling)
            {
                _pluginClient.ConnectedToServer -= PluginClient_ConnectedToServer;
                _pluginClient.MatchCreated -= PluginClient_MatchCreated;
                _pluginClient.MatchDeleted -= PluginClient_MatchDeleted;
                _pluginClient.PlayerInfoUpdated -= PluginClient_PlayerInfoUpdated;
                _pluginClient.FailedToConnectToServer -= PluginClient_FailedToConnectToServer;
                _pluginClient.StartLevel -= PluginClient_StartLevel;
                _pluginClient.LoadedSong -= PluginClient_LoadedSong;
                _pluginClient.ServerDisconnected -= PluginClient_ServerDisconnected;
            }
            if (removedFromHierarchy && _ongoingGameListView.isInViewControllerHierarchy)
            {
                SetLeftScreenViewController(null, ViewController.AnimationType.None);
                SetRightScreenViewController(null, ViewController.AnimationType.None);
            }

            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DismissRequested?.Invoke();
        }

        protected internal void SendDismissEvent()
        {
            DismissRequested?.Invoke();
        }

        protected virtual void PlaySong(PluginClient sender, StartLevelOptions level, MatchOptions match) { }
        protected abstract void Connected(PluginClient sender, Player player, ConnectResponse response);
        protected abstract void FailedToConnect(PluginClient sender, ConnectResponse response);
        protected virtual void SongLoaded(PluginClient sender, IBeatmapLevel level) { }
        protected virtual void PlayerUpdated(PluginClient sender, Player player) { }
        protected virtual void MatchCreated(PluginClient sender, Match match) { }
        protected virtual void MatchUpdated(PluginClient sender, Match match) { }
        protected virtual void MatchDeleted(PluginClient sender, Match match) { }
        protected virtual void ServerDisconnected(PluginClient sender) { }
    }
}