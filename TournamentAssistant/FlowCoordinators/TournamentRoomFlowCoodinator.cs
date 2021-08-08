using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistant.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace TournamentAssistant.FlowCoordinators
{
    internal class TournamentRoomFlowCoodinator : RoomFlowCoordinator
    {
        [Inject]
        private readonly PlayerListView _playerListView = null!;

        [Inject]
        private readonly SongDetailView _songDetailView = null!;

        [Inject]
        private readonly SplashScreenView _splashScreenView = null!;

        [Inject]
        private readonly SongSelectionView _songSelectionView = null!;

        [Inject]
        private readonly TeamSelectionView _teamSelectionView = null!;

        private FloatingScreen? _teamScreen;
        private Match? _match;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation)
            {
                SetTitle("Game Room");
                showBackButton = true;
            }
            if (addedToHierarchy)
            {
                _splashScreenView.Status = $"Connecting to \"{_host!.Name}\"...";
                ProvideInitialViewControllers(_splashScreenView);
            }

            _teamSelectionView.TeamSelected += TeamSelected;
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            _teamSelectionView.TeamSelected -= TeamSelected;
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        protected override void Connected(PluginClient sender, Player player, ConnectResponse response)
        {
            _splashScreenView.Status = $"Waiting for the coordinator to create your match...";
            if (player.Team.Id == System.Guid.Empty && sender.State.ServerSettings.EnableTeams)
            {
                if (_teamScreen == null)
                    _teamScreen = FloatingScreen.CreateFloatingScreen(new Vector2(100, 50), false, new Vector3(0f, 0.9f, 2.4f), Quaternion.Euler(30f, 0f, 0f));
                _teamSelectionView.SetTeams(new List<Team>(sender.State.ServerSettings.Teams));
                _teamScreen.SetRootViewController(_teamSelectionView, ViewController.AnimationType.None);
            }
        }

        protected override void FailedToConnect(PluginClient sender, ConnectResponse response)
        {
            _splashScreenView.Status = string.IsNullOrEmpty(response.Message) ? "Failed initial connection attempt, trying again..." : response.Message;
        }

        private void TeamSelected(Team team)
        {
            if (_pluginClient.Self is Player player)
            {
                var playerUpdate = new Event
                {
                    Type = Event.EventType.PlayerUpdated,
                    ChangedObject = player
                };
                _pluginClient.Send(new Packet(playerUpdate));
            }
        }

        protected override void PlayerUpdated(PluginClient sender, Player player)
        {
            if (_match != null)
            {
                var index = _match.Players.ToList().FindIndex(x => x.Id == player.Id);
                if (index >= 0)
                    _match.Players[index] = player;
            }
        }

        protected override void MatchCreated(PluginClient sender, Match match)
        {
            if (sender.Self is Player player)
            {
                if (_match == null && match.Players.Contains(player))
                {
                    _splashScreenView.Status = "Match has been created. Waiting for coordinator to select a song.";
                    var screenSystem = this.GetField<ScreenSystem, FlowCoordinator>("_screenSystem");
                    screenSystem.SetBackButton(false, true);
                }
            }
        }

        protected override void MatchDeleted(PluginClient sender, Match match)
        {
            if (_match == match)
            {
                if (_match == null)
                {
                    SwitchToWaitingForCoordinator();
                }
                else
                {
                    Dismiss();
                }
            }
        }

        protected override void ServerDisconnected(PluginClient sender)
        {
            Dismiss();
        }

        private void SwitchToWaitingForCoordinator()
        {
            if (_resultsViewController.isInViewControllerHierarchy)
            {
                _resultsViewController.GetField<Button, ResultsViewController>("_restartButton").gameObject.SetActive(true);
                _menuLightsManager.SetColorPreset(_defaultLightsPreset, false);
                DismissViewController(_resultsViewController, immediately: true);
            }
            else if (_songDetailView.isInViewControllerHierarchy)
            {
                DismissViewController(_songDetailView, immediately: true);
            }

            _splashScreenView.Status = "Waiting for the coordinator to create your match...";
            var screenSystem = this.GetField<ScreenSystem, FlowCoordinator>("_screenSystem");
            screenSystem.SetBackButton(true, true);
        }

        private void Dismiss()
        {
            if (_teamScreen != null && _teamSelectionView.isInViewControllerHierarchy)
            {
                _teamScreen.SetRootViewController(null, ViewController.AnimationType.Out);
            }
            SwitchToWaitingForCoordinator();
            BackButtonWasPressed(_splashScreenView);
        }
    }
}