using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class RoomCoordinator : FlowCoordinatorWithClient
    {
        public Match Match { get; set; }

        private SongSelection _songSelection;
        private SplashScreen _splashScreen;
        private PlayerList _playerList;
        private SongDetail _songDetail;

        private TeamSelection _teamSelection;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private CampaignFlowCoordinator _campaignFlowCoordinator;

        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _redLights;
        private MenuLightsPresetSO _defaultLights;

        private bool isHost;
        private bool tournamentMode;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                //Set up UI
                title = "Room Screen";
                showBackButton = true;

                _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _menuLightsManager = Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _campaignFlowCoordinator = Resources.FindObjectsOfTypeAll<CampaignFlowCoordinator>().First();
                _resultsViewController = Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _scoreLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsLightsPreset");
                _redLights = _campaignFlowCoordinator.GetField<MenuLightsPresetSO>("_newObjectiveLightsPreset");
                _defaultLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

                _songSelection = BeatSaberUI.CreateViewController<SongSelection>();
                _songSelection.SongSelected += songSelection_SongSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _songDetail.PlayPressed += songDetail_didPressPlayButtonEvent;
                _songDetail.DifficultyBeatmapChanged += songDetail_didChangeDifficultyBeatmapEvent;

                _playerList = BeatSaberUI.CreateViewController<PlayerList>();
            }
            if (activationType == ActivationType.AddedToHierarchy)
            {
                tournamentMode = Match == null;
                if (tournamentMode)
                {
                    _splashScreen.StatusText = $"Connecting to \"{Host.Name}\"...";
                    ProvideInitialViewControllers(_splashScreen);
                }
                else
                {
                    //If we're not in tournament mode, then a client connection has already been made
                    //by the room selection screen, so we can just assume Plugin.client isn't null
                    //NOTE: This is *such* a hack. Oh my god.
                    isHost = Match.Leader == Plugin.client.Self;
                    _songSelection.SetSongs(SongUtils.masterLevelList);
                    _playerList.Players = Match.Players;
                    _splashScreen.StatusText = "Waiting for the host to select a song...";

                    if (isHost)
                    {
                        ProvideInitialViewControllers(_songSelection, _playerList);
                    }
                    else
                    {
                        ProvideInitialViewControllers(_splashScreen, _playerList);
                    }
                }
            }

            //The ancestor sets up the server event listeners
            //It would be possible to recieve an event that does a ui update after this call
            //and before the rest of the ui is set up, if we did this at the top.
            //So, we do it last
            base.DidActivate(firstActivation, activationType);
        }

        public override void Dismiss()
        {
            if (_teamSelection?.screen) Destroy(_teamSelection.screen.gameObject);
            SwitchToWaitingForCoordinatorMode(); //Dismisses any presented view controllers
            base.Dismiss();
        }

        //If we're in tournament mode, we'll actually be alive when we recieve the initial
        //ConnectResponse. When we do, we need to check to see if Teams is enabled
        //so we can offer the team selection screen if needed.
        protected override void Client_ConnectedToServer(ConnectResponse response)
        {
            base.Client_ConnectedToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = "Waiting for the coordinator to create your match...";

                if ((response.Self as Player).Team.Guid == "0" && Plugin.client.State.ServerSettings.Teams.Length > 0)
                {
                    _teamSelection = BeatSaberUI.CreateViewController<TeamSelection>();
                    _teamSelection.TeamSelected += teamSelection_TeamSelected;
                    _teamSelection.SetTeams(new List<Team>(Plugin.client.State.ServerSettings.Teams));
                    ShowTeamSelection();
                }
            });
        }

        protected override void Client_FailedToConnectToServer(ConnectResponse response)
        {
            base.Client_FailedToConnectToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Message) ? response.Message : "Failed initial connection attempt, trying again...";
            });
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetail) DismissViewController(topViewController);
            else if (!_songDetail.GetField<bool>("_isInTransition"))
            {
                if (!tournamentMode)
                {
                    if (isHost) Plugin.client?.DeleteMatch(Match);
                    else
                    {
                        Match.Players = Match.Players.ToList().Except(new Player[] { Plugin.client.Self as Player }).ToArray();
                        Plugin.client?.UpdateMatch(Match);
                        Dismiss();
                    }
                }
                else Dismiss();
            }
        }

        public void ShowTeamSelection()
        {
            FloatingScreen screen = FloatingScreen.CreateFloatingScreen(new Vector2(100, 50), false, new Vector3(0f, 0.9f, 2.4f), Quaternion.Euler(30f, 0f, 0f));
            screen.SetRootViewController(_teamSelection, false);
        }

        private void SwitchToWaitingForCoordinatorMode()
        {
            if (Plugin.IsInMenu())
            {
                Match = null;

                //The results view and detail view aren't my own, they're the *real* views used in the
                //base game. As such, we should give them back them when we leave
                if (_resultsViewController.isInViewControllerHierarchy)
                {
                    _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                    _menuLightsManager.SetColorPreset(_defaultLights, false);
                    DismissViewController(_resultsViewController, immediately: true);
                }

                if (_songDetail.isInViewControllerHierarchy) DismissViewController(_songDetail, immediately: true);

                //Re-enable back button if it's disabled
                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                if (screenSystem != null)
                {
                    var backButton = screenSystem.GetField<Button>("_backButton");
                    if (!backButton.interactable) backButton.interactable = true;
                }

                _splashScreen.StatusText = "Waiting for the coordinator to create your match...";
            }
        }

        private void teamSelection_TeamSelected(Team team)
        {
            (Plugin.client.Self as Player).Team = team;

            var playerUpdate = new Event();
            playerUpdate.Type = Event.EventType.PlayerUpdated;
            playerUpdate.ChangedObject = Plugin.client.Self;
            Plugin.client.Send(new Packet(playerUpdate));

            Destroy(_teamSelection.screen.gameObject);
        }

        private void songSelection_SongSelected(IPreviewBeatmapLevel level)
        {
            //Load the song, then display the detail info
            SongUtils.LoadSong(level.levelID, (loadedLevel) =>
            {
                if (!_songDetail.isInViewControllerHierarchy)
                {
                    PresentViewController(_songDetail, () =>
                    {
                        _songDetail.SetHost(isHost);
                        _songDetail.SetSelectedSong(loadedLevel);
                    });
                }
                else
                {
                    _songDetail.SetHost(isHost);
                    _songDetail.SetSelectedSong(loadedLevel);
                }

                //Tell the other players to download the song, if we're host
                if (isHost)
                {
                    var loadSong = new LoadSong();
                    loadSong.LevelId = loadedLevel.levelID;

                    //Send updated download status
                    (Plugin.client.Self as Player).DownloadState = Player.DownloadStates.Downloaded;

                    var playerUpdate = new Event();
                    playerUpdate.Type = Event.EventType.PlayerUpdated;
                    playerUpdate.ChangedObject = Plugin.client.Self;
                    Plugin.client.Send(new Packet(playerUpdate));

                    //We don't want to recieve this since it would cause an infinite song loading loop.
                    //Our song is already loaded inherently since we're selecting it as the host
                    Plugin.client.Send(Match.Players.Except(new Player[] { Plugin.client.Self as Player }).Select(x => x.Guid).ToArray(), new Packet(loadSong));
                }
            });
        }

        private void songDetail_didChangeDifficultyBeatmapEvent(IDifficultyBeatmap beatmap)
        {
            var level = beatmap.level;

            //Assemble new match info and update the match
            var matchLevel = new PreviewBeatmapLevel()
            {
                LevelId = level.levelID,
                Name = level.songName
            };

            List<Characteristic> characteristics = new List<Characteristic>();
            foreach (var beatmapSet in level.previewDifficultyBeatmapSets)
            {
                characteristics.Add(new Characteristic()
                {
                    SerializedName = beatmapSet.beatmapCharacteristic.serializedName,
                    Difficulties = beatmapSet.beatmapDifficulties.Select(x => (SharedConstructs.BeatmapDifficulty)x).ToArray()
                });
            }
            matchLevel.Characteristics = characteristics.ToArray();
            Match.SelectedLevel = matchLevel;
            Match.SelectedCharacteristic = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            Match.SelectedDifficulty = (SharedConstructs.BeatmapDifficulty)beatmap.difficulty;

            if (isHost)
            {
                Plugin.client.UpdateMatch(Match);
            }
        }

        private void songDetail_didPressPlayButtonEvent(IBeatmapLevel _, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            var gm = new TournamentAssistantShared.Models.GameplayModifiers();

            var playSong = new PlaySong();
            playSong.Beatmap = new Beatmap();
            playSong.Beatmap.Characteristic = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == characteristic.serializedName);
            playSong.Beatmap.Difficulty = (SharedConstructs.BeatmapDifficulty)difficulty;
            playSong.Beatmap.LevelId = Match.SelectedLevel.LevelId;

            playSong.GameplayModifiers = gm;
            playSong.PlayerSettings = new TournamentAssistantShared.Models.PlayerSpecificSettings();

            playSong.FloatingScoreboard = true;

            Plugin.client.Send(Match.Players.Select(x => x.Guid).ToArray(), new Packet(playSong));
        }

        protected override void Client_PlayerInfoUpdated(Player player)
        {
            base.Client_PlayerInfoUpdated(player);

            if (Match != null)
            {
                //If the updated player is part of our match 
                var index = Match.Players.ToList().FindIndex(x => x.Guid == player.Guid);
                if (index >= 0) Match.Players[index] = player;
            }
        }

        protected override void Client_MatchCreated(Match match)
        {
            base.Client_MatchCreated(match);

            if (tournamentMode && match.Players.Contains(Plugin.client.Self))
            {
                Match = match;

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //Player shouldn't be able to back out of a coordinated match
                    var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                    screenSystem.GetField<Button>("_backButton").interactable = false;

                    _splashScreen.StatusText = "Match has been created. Waiting for coordinator to select a song.";
                });
            }
        }

        protected override void Client_MatchInfoUpdated(Match match)
        {
            base.Client_MatchInfoUpdated(match);

            if (match == Match)
            {
                Match = match;
                _playerList.Players = match.Players;

                if (!isHost && _songDetail && _songDetail.isInViewControllerHierarchy && match.SelectedLevel != null && match.SelectedCharacteristic != null)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        //`CurrentlySelectedDifficulty` is reset by SetSelectedCharacteristic, so we save it here
                        //Usually this is intended behavior so that a new difficulty is selected
                        //when the new characteristic doesn't have a corresponding difficulty to the one
                        //that was previously selected. However... We don't want that here. Here, we
                        //know that the CurrentlySelectedDifficulty *should* be available on the new
                        //characteristic, if the coordinator/leader hasn't messed up, and often changes simultaneously
                        var selectedDifficulty = (int)match.SelectedDifficulty;

                        _songDetail.SetSelectedCharacteristic(match.SelectedCharacteristic.SerializedName);
                        _songDetail.SetSelectedDifficulty(selectedDifficulty);
                    });
                }
            }
        }

        protected override void Client_MatchDeleted(Match match)
        {
            base.Client_MatchDeleted(match);

            //If the match is destroyed while we're in here, back out
            if (match == Match)
            {
                if (Plugin.IsInMenu())
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        if (tournamentMode) SwitchToWaitingForCoordinatorMode();
                        else Dismiss();
                    });
                }
                else
                {
                    //If the player is in-game... boot them out... Yeah.
                    //Harsh, but... Expected functionality
                    PlayerUtils.ReturnToMenu();
                }
            }
        }

        protected override void Client_LoadedSong(IBeatmapLevel level)
        {
            base.Client_LoadedSong(level);

            if (Plugin.IsInMenu())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //If the player is still on the results screen, go ahead and boot them out
                    if (_resultsViewController.isInViewControllerHierarchy) resultsViewController_continueButtonPressedEvent(null);

                    songSelection_SongSelected(level);
                });
            }
        }

        protected override void Client_PlaySong(IPreviewBeatmapLevel desiredLevel, BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme, bool useFloatingScoreboard = false, bool useSync = false)
        {
            base.Client_PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers, playerSpecificSettings, overrideEnvironmentSettings, colorScheme, useFloatingScoreboard, useSync);

            //Set up per-play settings
            Plugin.UseSyncController = useSync;
            Plugin.UseFloatingScoreboard = useFloatingScoreboard;

            //Reset score
            (Plugin.client.Self as Player).Score = 0;
            var playerUpdate = new Event();
            playerUpdate.Type = Event.EventType.PlayerUpdated;
            playerUpdate.ChangedObject = Plugin.client.Self;
            Plugin.client.Send(new Packet(playerUpdate));

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                //If the player is still on the results screen, go ahead and boot them out
                if (_resultsViewController.isInViewControllerHierarchy) resultsViewController_continueButtonPressedEvent(null);

                SongUtils.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSpecificSettings, SongFinished);
            });
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = (standardLevelScenesTransitionSetupData.sceneSetupDataArray.First(x => x is GameplayCoreSceneSetupData) as GameplayCoreSceneSetupData).difficultyBeatmap;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            //Send final score to Host
            if (Plugin.client.Connected)
            {
                Logger.Debug($"SENDING RESULTS: {results.modifiedScore}");

                var songFinished = new SongFinished();
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Passed;
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed) songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Failed;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit) songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Quit;

                songFinished.User = Plugin.client.Self as Player;

                songFinished.Beatmap = new Beatmap();
                songFinished.Beatmap.LevelId = map.level.levelID;
                songFinished.Beatmap.Difficulty = (SharedConstructs.BeatmapDifficulty)map.difficulty;
                songFinished.Beatmap.Characteristic = new Characteristic();
                songFinished.Beatmap.Characteristic.SerializedName = map.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
                songFinished.Beatmap.Characteristic.Difficulties = map.parentDifficultyBeatmapSet.difficultyBeatmaps.Select(x => (SharedConstructs.BeatmapDifficulty)x.difficulty).ToArray();

                songFinished.Score = results.modifiedScore;

                Plugin.client.Send(new Packet(songFinished));
            }

            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.None)
            {
                _menuLightsManager.SetColorPreset(_scoreLights, true);
                _resultsViewController.Init(results, map, false, highScore);
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(false);
                _resultsViewController.continueButtonPressedEvent += resultsViewController_continueButtonPressedEvent;
                PresentViewController(_resultsViewController, null, true);
            }
            else if (ShouldDismissOnReturnToMenu) Dismiss();
            else if (!Plugin.client.State.Matches.Contains(Match))
            {
                if (tournamentMode) SwitchToWaitingForCoordinatorMode();
                else Dismiss();
            }
        }

        private void resultsViewController_continueButtonPressedEvent(ResultsViewController _)
        {
            _resultsViewController.continueButtonPressedEvent -= resultsViewController_continueButtonPressedEvent;
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);

            if (ShouldDismissOnReturnToMenu) Dismiss();
            else if (!Plugin.client.State.Matches.Contains(Match))
            {
                if (tournamentMode) SwitchToWaitingForCoordinatorMode();
                else Dismiss();
            }
        }
    }
}
