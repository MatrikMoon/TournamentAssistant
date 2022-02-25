using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Interop;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class RoomCoordinator : FlowCoordinatorWithClient
    {
        public Match Match { get; set; }
        public bool TournamentMode { get; private set; }

        private SongSelection _songSelection;
        private SplashScreen _splashScreen;
        private PlayerList _playerList;
        private SongDetail _songDetail;

        private TeamSelection _teamSelection;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;

        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _defaultLights;

        private bool isHost;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                //Set up UI
                SetTitle("Game Room", ViewController.AnimationType.None);

                showBackButton = true;

                _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _menuLightsManager = Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _resultsViewController = Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _scoreLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsClearedLightsPreset");
                _defaultLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

                _songSelection = BeatSaberUI.CreateViewController<SongSelection>();
                _songSelection.SongSelected += SongSelection_SongSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _songDetail.PlayPressed += SongDetail_didPressPlayButtonEvent;
                _songDetail.DifficultyBeatmapChanged += SongDetail_didChangeDifficultyBeatmapEvent;

                _playerList = BeatSaberUI.CreateViewController<PlayerList>();
            }

            if (addedToHierarchy)
            {
                TournamentMode = Match == null;
                if (TournamentMode)
                {
                    _splashScreen.StatusText = $"Connecting to \"{Host.Name}\"...";
                    ProvideInitialViewControllers(_splashScreen);
                }
                else
                {
                    //If we're not in tournament mode, then a client connection has already been made
                    //by the room selection screen, so we can just assume Plugin.client isn't null
                    //NOTE: This is *such* a hack. Oh my god.
                    isHost = Match.Leader.UserEquals(Plugin.client.Self);
                    _songSelection.SetSongs(SongUtils.masterLevelList);
                    _playerList.Players = Match.Players.ToArray();
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
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
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
        protected override async Task Client_ConnectedToServer(ConnectResponse response)
        {
            await base.Client_ConnectedToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = "Waiting for the coordinator to create your match...";

                if (Plugin.client.State.ServerSettings.EnableTeams)
                {
                    _teamSelection = BeatSaberUI.CreateViewController<TeamSelection>();
                    _teamSelection.TeamSelected += TeamSelection_TeamSelected;
                    _teamSelection.SetTeams(new List<Team>(Plugin.client.State.ServerSettings.Teams));
                    ShowTeamSelection();
                }
            });
        }

        protected override async Task Client_FailedToConnectToServer(ConnectResponse response)
        {
            await base.Client_FailedToConnectToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Response.Message)
                    ? response.Response.Message
                    : "Failed initial connection attempt, trying again...";
            });
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetail) DismissViewController(topViewController);
            else if (!_songDetail.GetField<bool>("_isInTransition"))
            {
                if (!TournamentMode)
                {
                    if (isHost) Plugin.client?.DeleteMatch(Match);
                    else
                    {
                        var playerToRemove =
                            Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
                        Match.Players.Remove(playerToRemove);
                        Plugin.client?.UpdateMatch(Match);
                        Dismiss();
                    }
                }
                else Dismiss();
            }
        }

        public void ShowTeamSelection()
        {
            FloatingScreen screen = FloatingScreen.CreateFloatingScreen(new Vector2(100, 50), false,
                new Vector3(0f, 0.9f, 2.4f), Quaternion.Euler(30f, 0f, 0f));
            screen.SetRootViewController(_teamSelection, ViewController.AnimationType.None);
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

        private void TeamSelection_TeamSelected(Team team)
        {
            var player = Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
            player.Team = team;

            var playerUpdate = new Event
            {
                player_updated_event = new Event.PlayerUpdatedEvent
                {
                    Player = player
                }
            };
            Plugin.client.Send(new Packet
            {
                Event = playerUpdate
            });

            Destroy(_teamSelection.screen.gameObject);
        }

        private void SongSelection_SongSelected(GameplayParameters parameters) =>
            SongSelection_SongSelected(parameters.Beatmap.LevelId);

        private void SongSelection_SongSelected(string levelId)
        {
            //Load the song, then display the detail info
            SongUtils.LoadSong(levelId, (loadedLevel) =>
            {
                if (!_songDetail.isInViewControllerHierarchy)
                {
                    PresentViewController(_songDetail, () =>
                    {
                        _songDetail.DisableCharacteristicControl = !isHost;
                        _songDetail.DisableDifficultyControl = !isHost;
                        _songDetail.DisablePlayButton = !isHost;
                        _songDetail.SetSelectedSong(loadedLevel);
                    });
                }
                else
                {
                    _songDetail.DisableCharacteristicControl = !isHost;
                    _songDetail.DisableDifficultyControl = !isHost;
                    _songDetail.DisablePlayButton = !isHost;
                    _songDetail.SetSelectedSong(loadedLevel);
                }

                //Tell the other players to download the song, if we're host
                if (isHost)
                {
                    var loadSong = new LoadSong
                    {
                        LevelId = loadedLevel.levelID
                    };

                    //Send updated download status
                    var player = Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
                    player.DownloadState = Player.DownloadStates.Downloaded;

                    var playerUpdate = new Event
                    {
                        player_updated_event = new Event.PlayerUpdatedEvent
                        {
                            Player = player
                        }
                    };
                    Plugin.client.Send(new Packet
                    {
                        Event = playerUpdate
                    });

                    //We don't want to recieve this since it would cause an infinite song loading loop.
                    //Our song is already loaded inherently since we're selecting it as the host
                    Plugin.client.Send(
                        Match.Players.Except(new Player[] {player}).Select(x => Guid.Parse(x.User.Id)).ToArray(),
                        new Packet
                        {
                            LoadSong = loadSong
                        });
                }
            });
        }

        private void SongDetail_didChangeDifficultyBeatmapEvent(IDifficultyBeatmap beatmap)
        {
            var level = beatmap.level;

            //Assemble new match info and update the match
            var matchLevel = new PreviewBeatmapLevel()
            {
                LevelId = level.levelID,
                Name = level.songName
            };

            List<Characteristic> characteristics = new();
            foreach (var beatmapSet in level.previewDifficultyBeatmapSets)
            {
                var characteristic = new Characteristic()
                {
                    SerializedName = beatmapSet.beatmapCharacteristic.serializedName
                };
                characteristic.Difficulties = beatmapSet.beatmapDifficulties.Select(x => (int) x).ToArray();
                characteristics.Add(characteristic);
            }

            matchLevel.Characteristics.AddRange(characteristics);
            Match.SelectedLevel = matchLevel;
            Match.SelectedCharacteristic = Match.SelectedLevel.Characteristics.First(x =>
                x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            Match.SelectedDifficulty = (int) beatmap.difficulty;

            if (isHost)
            {
                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Plugin.client.UpdateMatch(Match);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        private void SongDetail_didPressPlayButtonEvent(IBeatmapLevel _, BeatmapCharacteristicSO characteristic,
            BeatmapDifficulty difficulty)
        {
            var playSong = new PlaySong();
            var gameplayParameters = new GameplayParameters
            {
                Beatmap = new Beatmap()
            };
            gameplayParameters.Beatmap.Characteristic =
                Match.SelectedLevel.Characteristics.First(x => x.SerializedName == characteristic.serializedName);
            gameplayParameters.Beatmap.Difficulty = (int) difficulty;
            gameplayParameters.Beatmap.LevelId = Match.SelectedLevel.LevelId;

            gameplayParameters.GameplayModifiers = new TournamentAssistantShared.Models.GameplayModifiers();
            gameplayParameters.PlayerSettings = new TournamentAssistantShared.Models.PlayerSpecificSettings();

            playSong.GameplayParameters = gameplayParameters;
            playSong.FloatingScoreboard = true;

            Plugin.client.Send(Match.Players.Select(x => Guid.Parse(x.User.Id)).ToArray(), new Packet
            {
                PlaySong = playSong
            });
        }

        protected override async Task Client_PlayerInfoUpdated(Player player)
        {
            await base.Client_PlayerInfoUpdated(player);

            if (Match != null)
            {
                //If the updated player is part of our match 
                var index = Match.Players.ToList().FindIndex(x => x.User.Id == player.User.Id);
                if (index >= 0) Match.Players[index] = player;
            }
        }

        protected override async Task Client_MatchCreated(Match match)
        {
            await base.Client_MatchCreated(match);

            var self = Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
            if (TournamentMode && match.Players.ContainsPlayer(self))
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

        protected override async Task Client_MatchInfoUpdated(Match match)
        {
            await base.Client_MatchInfoUpdated(match);

            if (match.MatchEquals(Match))
            {
                Match = match;
                _playerList.Players = match.Players.ToArray();

                var self = Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
                if (!isHost && !match.Players.ContainsPlayer(self))
                {
                    RemoveSelfFromMatch();
                }
                else if (!isHost && _songDetail && _songDetail.isInViewControllerHierarchy &&
                         match.SelectedLevel != null && match.SelectedCharacteristic != null)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        //`CurrentlySelectedDifficulty` is reset by SetSelectedCharacteristic, so we save it here
                        //Usually this is intended behavior so that a new difficulty is selected
                        //when the new characteristic doesn't have a corresponding difficulty to the one
                        //that was previously selected. However... We don't want that here. Here, we
                        //know that the CurrentlySelectedDifficulty *should* be available on the new
                        //characteristic, if the coordinator/leader hasn't messed up, and often changes simultaneously
                        var selectedDifficulty = (int) match.SelectedDifficulty;

                        _songDetail.SetSelectedCharacteristic(match.SelectedCharacteristic.SerializedName);

                        if (match.SelectedCharacteristic.Difficulties.Contains(selectedDifficulty))
                        {
                            _songDetail.SetSelectedDifficulty(selectedDifficulty);
                        }
                    });
                }
            }
        }

        protected override async Task Client_MatchDeleted(Match match)
        {
            await base.Client_MatchDeleted(match);

            //If the match is destroyed while we're in here, back out
            if (match.MatchEquals(match))
            {
                RemoveSelfFromMatch();
            }
        }

        private void RemoveSelfFromMatch()
        {
            if (Plugin.IsInMenu())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (TournamentMode) SwitchToWaitingForCoordinatorMode();
                    else Dismiss();
                });
            }
            else
            {
                //If the player is in-game... boot them out... Yeah.
                //Harsh, but... Expected functionality
                //IN-TESTING: Temporarily disabled. Too many matches being accidentally ended by curious coordinators
                //PlayerUtils.ReturnToMenu();
            }
        }

        protected override async Task Client_LoadedSong(IBeatmapLevel level)
        {
            await base.Client_LoadedSong(level);

            if (Plugin.IsInMenu())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //If the player is still on the results screen, go ahead and boot them out
                    if (_resultsViewController.isInViewControllerHierarchy)
                        ResultsViewController_continueButtonPressedEvent(null);

                    SongSelection_SongSelected(level.levelID);
                });
            }
        }

        protected override async Task Client_PlaySong(IPreviewBeatmapLevel desiredLevel,
            BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty,
            GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings,
            OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme,
            bool useFloatingScoreboard = false, bool useSync = false, bool disableFail = false,
            bool disablePause = false)
        {
            await base.Client_PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers,
                playerSpecificSettings, overrideEnvironmentSettings, colorScheme, useFloatingScoreboard, useSync,
                disableFail, disablePause);

            //Set up per-play settings
            Plugin.UseSync = useSync;
            Plugin.UseFloatingScoreboard = useFloatingScoreboard;
            Plugin.DisableFail = disableFail;
            Plugin.DisablePause = disablePause;

            //Reset score
            var player = Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
            player.Score = 0;
            player.Accuracy = 0;
            var playerUpdate = new Event
            {
                player_updated_event = new Event.PlayerUpdatedEvent
                {
                    Player = player
                }
            };
            await Plugin.client.Send(new Packet
            {
                Event = playerUpdate
            });

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                //If the player is still on the results screen, go ahead and boot them out
                if (_resultsViewController.isInViewControllerHierarchy)
                    ResultsViewController_continueButtonPressedEvent(null);

                SongUtils.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, overrideEnvironmentSettings,
                    colorScheme, gameplayModifiers, playerSpecificSettings, SongFinished);
            });
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData,
            LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map =
                (standardLevelScenesTransitionSetupData.sceneSetupDataArray.First(x => x is GameplayCoreSceneSetupData)
                    as GameplayCoreSceneSetupData).difficultyBeatmap;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            //Disable HMD Only if it was enabled (or even if not. Doesn't matter to me)
            var customNotes = IPA.Loader.PluginManager.GetPluginFromId("CustomNotes");
            if (customNotes != null)
            {
                DisableHMDOnly();
            }

            //Send final score to Host
            if (Plugin.client.Connected)
            {
                Logger.Debug($"SENDING RESULTS: {results.modifiedScore}");

                var songFinished = new SongFinished();
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
                    songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Passed;
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                    songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Failed;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit)
                    songFinished.Type = TournamentAssistantShared.Models.Packets.SongFinished.CompletionType.Quit;

                var player = Plugin.client.State.Players.FirstOrDefault(x => x.User.UserEquals(Plugin.client.Self));
                songFinished.Player = player;
                songFinished.Beatmap = new Beatmap
                {
                    LevelId = map.level.levelID,
                    Difficulty = (int) map.difficulty,
                    Characteristic = new Characteristic()
                };
                songFinished.Beatmap.Characteristic.SerializedName = map.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
                songFinished.Beatmap.Characteristic.Difficulties = map.parentDifficultyBeatmapSet.difficultyBeatmaps.Select(x => (int) x.difficulty).ToArray();
                songFinished.Score = results.modifiedScore;

                Plugin.client.Send(new Packet
                {
                    SongFinished = songFinished
                });
            }

            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Incomplete)
            {
                _menuLightsManager.SetColorPreset(_scoreLights, true);
                _resultsViewController.Init(results, map, false, highScore);
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(false);
                _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                PresentViewController(_resultsViewController, immediately: true);
            }
            else if (ShouldDismissOnReturnToMenu) Dismiss();
            else if (!Plugin.client.State.Matches.ContainsMatch(Match))
            {
                if (TournamentMode) SwitchToWaitingForCoordinatorMode();
                else Dismiss();
            }
        }

        private void ResultsViewController_continueButtonPressedEvent(ResultsViewController _)
        {
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);

            if (ShouldDismissOnReturnToMenu) Dismiss();
            else if (!Plugin.client.State.Matches.ContainsMatch(Match))
            {
                if (TournamentMode) SwitchToWaitingForCoordinatorMode();
                else Dismiss();
            }
        }

        //Broken off so that if custom notes isn't installed, we don't try to load anything from it
        private static void DisableHMDOnly()
        {
            CustomNotesInterop.DisableHMDOnly();
        }
    }
}