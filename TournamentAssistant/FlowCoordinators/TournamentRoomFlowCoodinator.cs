using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistant.Models;
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
        private bool _isHost;

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
                if (_match == null)
                {
                    _splashScreenView.Status = $"Connecting to \"{_host!.Name}\"...";
                    ProvideInitialViewControllers(_splashScreenView);
                }

                _songDetailView.ClickedPlay += SongDetailView_ClickedPlay;
                _songDetailView.BeatmapChanged += SongDetailView_BeatmapChanged;
                _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
            }

            _teamSelectionView.TeamSelected += TeamSelected;
        }

        private void ResultsViewController_continueButtonPressedEvent(ResultsViewController rvc)
        {
            _resultsViewController.GetField<Button, ResultsViewController>("_restartButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLightsPreset, true);
            DismissViewController(_resultsViewController);

            if (!_pluginClient.Connected)
            {
                Dismiss();
            }
            else if (_pluginClient.State.Matches.Contains(_match))
            {
                if (_match == null)
                    SwitchToWaitingForCoordinator();
                else
                    Dismiss();
            }
        }

        private void SongDetailView_ClickedPlay(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            if (_match == null)
                return;

            var playSong = new PlaySong();
            var gameplayParameters = new GameplayParameters
            {
                Beatmap = new Beatmap()
            };
            gameplayParameters.Beatmap.Characteristic = _match.SelectedLevel.Characteristics.First(x => x.SerializedName == characteristic.serializedName);
            gameplayParameters.Beatmap.Difficulty = (SharedConstructs.BeatmapDifficulty)difficulty;
            gameplayParameters.Beatmap.LevelId = _match.SelectedLevel.LevelId;

            gameplayParameters.GameplayModifiers = new TournamentAssistantShared.Models.GameplayModifiers();
            gameplayParameters.PlayerSettings = new TournamentAssistantShared.Models.PlayerSpecificSettings();

            playSong.GameplayParameters = gameplayParameters;
            playSong.FloatingScoreboard = true;

            _pluginClient.Send(_match.Players.Select(p => p.Id).ToArray(), new Packet(playSong));
        }

        private void SongDetailView_BeatmapChanged(IDifficultyBeatmap beatmap)
        {
            if (_match == null)
                return;

            var level = beatmap.level;
            var matchLevel = new PreviewBeatmapLevel
            {
                LevelId = level.levelID,
                Name = level.songName
            };

            List<Characteristic> characteristics = new();
            foreach (var beatmapSet in level.previewDifficultyBeatmapSets)
            {
                characteristics.Add(new Characteristic()
                {
                    SerializedName = beatmapSet.beatmapCharacteristic.serializedName,
                    Difficulties = beatmapSet.beatmapDifficulties.Select(x => (SharedConstructs.BeatmapDifficulty)x).ToArray()
                });
            }
            matchLevel.Characteristics = characteristics.ToArray();
            _match.SelectedLevel = matchLevel;
            _match.SelectedCharacteristic = _match.SelectedLevel.Characteristics.First(x => x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            _match.SelectedDifficulty = (SharedConstructs.BeatmapDifficulty)beatmap.difficulty;

            if (_isHost)
                _pluginClient.UpdateMatch(_match);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
                _songDetailView.BeatmapChanged -= SongDetailView_BeatmapChanged;
                _songDetailView.ClickedPlay -= SongDetailView_ClickedPlay;
            }
            _teamSelectionView.TeamSelected -= TeamSelected;
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        protected override void Connected(PluginClient sender, Player player, ConnectResponse response)
        {
            _splashScreenView.Status = $"Waiting for the coordinator to create your match...";
            if (player.Team.Id == Guid.Empty && sender.State.ServerSettings.EnableTeams)
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
            _isHost = match.Leader == sender.Self;
            _playerListView.SetPlayers(match.Players.ToList());
        }

        protected override void MatchUpdated(PluginClient sender, Match match)
        {
            if (_match == match)
            {
                _match = match;
                _playerListView.SetPlayers(match.Players.ToList());

                if (!_isHost && _songDetailView.isInViewControllerHierarchy && _match.SelectedLevel != null && _match.SelectedCharacteristic != null)
                {
                    _songDetailView.SetSelectedCharacteristic(match.SelectedCharacteristic.SerializedName);
                    _songDetailView.SetSelectedDifficulty((int)match.SelectedDifficulty);
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

        protected override void SongLoaded(PluginClient sender, IBeatmapLevel level)
        {
            PresentSongDetails(level);
        }

        private void PresentSongDetails(IBeatmapLevel level)
        {
            _songDetailView.ShowCharacteristics = _isHost;
            _songDetailView.ShowDifficulties = _isHost;
            _songDetailView.ShowPlayButton = _isHost;

            if (!_songDetailView.isInViewControllerHierarchy)
            {
                PresentViewController(_songDetailView, () =>
                {
                    _songDetailView.SetSelectedSong(level);
                });
            }
            else
            {
                _songDetailView.SetSelectedSong(level);
            }

            if (_isHost)
            {
                var loadSong = new LoadSong
                {
                    LevelId = level.levelID
                };

                if (_pluginClient.Self is Player player)
                {
                    player.DownloadState = Player.DownloadStates.Downloaded;
                    var pUpdate = new Event
                    {
                        Type = Event.EventType.PlayerUpdated,
                        ChangedObject = _pluginClient.Self
                    };
                    _pluginClient.Send(new Packet(pUpdate));
                    if (_match != null)
                    {
                        _pluginClient.Send(_match.Players.Except(new Player[] { player }).Select(p => p.Id).ToArray(), new Packet(loadSong));
                    }
                }
            }
        }

        protected override void PlaySong(PluginClient sender, StartLevelOptions level, MatchOptions match)
        {
            if (sender.Self is Player player && level.Level is IBeatmapLevel beatmap)
            {
                player.Score = 0;
                player.Accuracy = 0;

                var pUpdate = new Event
                {
                    Type = Event.EventType.PlayerUpdated,
                    ChangedObject = sender.Self
                };
                _pluginClient.Send(new Packet(pUpdate));

                if (_resultsViewController.isInViewControllerHierarchy)
                {
                    ResultsViewController_continueButtonPressedEvent(_resultsViewController);
                }

                _pluginClient.ActiveMatch = _match;
                _pluginClient.ActiveMatchOptions = match;
                _menuTransitionsHelper.StartStandardLevel(
                    "Solo",
                    beatmap.beatmapLevelData.GetDifficultyBeatmap(level.Characteristic, level.Difficulty),
                    beatmap,
                    level.Environment,
                    level.Colors,
                    level.Modifiers ?? new GameplayModifiers(),
                    level.Player ?? new PlayerSpecificSettings(),
                    null,
                    "Menu",
                    false,
                    null,
                    SongFinished
                );
            }
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = (standardLevelScenesTransitionSetupData.sceneSetupDataArray.First(x => x is GameplayCoreSceneSetupData) as GameplayCoreSceneSetupData)!.difficultyBeatmap;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;


            // Send final score to Host
            if (_pluginClient.Connected && _pluginClient.Self is Player player)
            {
                _siraLog.Debug($"SENDING RESULTS: {results.modifiedScore}");

                var songFinished = new SongFinished();
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Passed;
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed) songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Failed;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit) songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Quit;

                songFinished.User = player;

                songFinished.Beatmap = new Beatmap
                {
                    LevelId = map.level.levelID,
                    Difficulty = (SharedConstructs.BeatmapDifficulty)map.difficulty,
                    Characteristic = new Characteristic()
                };
                songFinished.Beatmap.Characteristic.SerializedName = map.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
                songFinished.Beatmap.Characteristic.Difficulties = map.parentDifficultyBeatmapSet.difficultyBeatmaps.Select(x => (SharedConstructs.BeatmapDifficulty)x.difficulty).ToArray();

                songFinished.Score = results.modifiedScore;

                _pluginClient.Send(new Packet(songFinished));
            }

            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.None)
            {
                _menuLightsManager.SetColorPreset(_resultsClearedLightsPreset, true);
                _resultsViewController.Init(results, map, false, highScore);
                _resultsViewController.GetField<Button, ResultsViewController>("_restartButton").gameObject.SetActive(false);
                _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                PresentViewController(_resultsViewController, immediately: true);
            }
            else if (!_pluginClient.Connected)
                Dismiss();
            else if (!_pluginClient.State.Matches.Contains(_match))
            {
                if (_match == null)
                    SwitchToWaitingForCoordinator();
                else
                    Dismiss();
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
            BackButtonWasPressed(_splashScreenView);
        }

        private void CleanupAndClose()
        {
            if (_teamScreen != null && _teamSelectionView.isInViewControllerHierarchy)
            {
                _teamScreen.SetRootViewController(null, ViewController.AnimationType.Out);
            }
            SwitchToWaitingForCoordinator();
            SendDismissEvent();
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetailView)
            {
                base.BackButtonWasPressed(topViewController);
            }
            else if (!_songDetailView.GetField<bool, ViewController>("_isInTransition"))
            {
                if (_match != null)
                {
                    if (_isHost)
                    {
                        _pluginClient.DeleteMatch(_match);
                    }
                    else if (_pluginClient.Self is Player player)
                    {
                        _match.Players = _match.Players.Where(p => p != player).ToArray();
                        _pluginClient.UpdateMatch(_match);
                        CleanupAndClose();
                    }
                }
                else
                {
                    CleanupAndClose();
                }
            }
        }
    }
}