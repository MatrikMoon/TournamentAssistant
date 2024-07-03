using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using IPA.Utilities;
using IPA.Utilities.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Interop;
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
        private ServerMessage _serverMessage;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private StandardLevelDetailViewController _standardLevelDetailViewController;
        private StandardLevelDetailView _standardLevelDetailView;

        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _defaultLights;

        private bool isHost;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                //Set up UI
                SetTitle(Plugin.GetLocalized("game_room"), ViewController.AnimationType.None);

                showBackButton = true;

                _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _menuLightsManager = Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _standardLevelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
                _standardLevelDetailView = _standardLevelDetailViewController.GetField<StandardLevelDetailView, StandardLevelDetailViewController>("_standardLevelDetailView");
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
                    _splashScreen.StatusText = $"{Plugin.GetLocalized("connecting_to")} \"{Host.Name}\"...";
                    ProvideInitialViewControllers(_splashScreen);
                }
                else
                {
                    //If we're not in tournament mode, then a client connection has already been made
                    //by the room selection screen, so we can just assume Plugin.client isn't null
                    //NOTE: This is *such* a hack. Oh my god.
                    isHost = Match.Leader == Plugin.client.Self.Guid;
                    _songSelection.SetSongs(SongUtils.masterLevelList);
                    _playerList.Players = Plugin.client.State.Users.Where(x => Match.AssociatedUsers.Contains(x.Guid) && x.ClientType == User.ClientTypes.Player).ToArray();
                    _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_host_to_select_song");

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
            if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);
            SwitchToWaitingForCoordinatorMode(); //Dismisses any presented view controllers
            base.Dismiss();
        }

        //If we're in tournament mode, we'll actually be alive when we recieve the initial
        //ConnectResponse. When we do, we need to check to see if Teams is enabled
        //so we can offer the team selection screen if needed.
        protected override async Task ConnectedToServer(Response.Connect response)
        {
            await base.ConnectedToServer(response);

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");

                if (Plugin.client.State.ServerSettings.EnableTeams)
                {
                    _teamSelection = BeatSaberUI.CreateViewController<TeamSelection>();
                    _teamSelection.TeamSelected += TeamSelection_TeamSelected;
                    _teamSelection.SetTeams(new List<Team>(Plugin.client.State.ServerSettings.Teams));
                    ShowTeamSelection();
                }
            });
        }

        protected override async Task FailedToConnectToServer(Response.Connect response)
        {
            await base.FailedToConnectToServer(response);

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Message)
                    ? response.Message
                    : Plugin.GetLocalized("failed_initial_attempt");
            });
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetail) DismissViewController(topViewController);
            else if (!_songDetail.isInTransition)
            {
                if (!TournamentMode)
                {
                    if (isHost) Plugin.client?.DeleteMatch(Match);
                    else
                    {
                        Match.AssociatedUsers.Remove(Plugin.client.Self.Guid);
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

        protected override async Task ShowModal(Command.ShowModal msg)
        {
            await base.ShowModal(msg);

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);
                _serverMessage = BeatSaberUI.CreateViewController<ServerMessage>();
                _serverMessage.SetMessage(msg);
                _serverMessage.OptionSelected += ModalResponse;
                FloatingScreen screen = FloatingScreen.CreateFloatingScreen(new Vector2(100, 250), false,
                    new Vector3(0f, 1.2f, 3f), Quaternion.Euler(10f, 0f, 0f));
                screen.SetRootViewController(_serverMessage, ViewController.AnimationType.None);
            });
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

                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");
            }
        }

        private void ModalResponse(ModalOption response, string modalId)
        {
            if (response != null)
            {
                Plugin.client.Send(Match.AssociatedUsers.Where(x => Plugin.client.GetUserByGuid(x).ClientType != User.ClientTypes.Player).Select(Guid.Parse).ToArray(), new Packet
                {
                    Response = new Response
                    {
                        modal = new Response.Modal
                        {
                            ModalId = modalId.ToString(),
                            Value = response.Value
                        }
                    }
                });

            }
            if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);
            _serverMessage.OptionSelected -= ModalResponse;
            _serverMessage = null;
        }

        private void TeamSelection_TeamSelected(Team team)
        {
            var player = Plugin.client.State.Users.FirstOrDefault(x => x.UserEquals(Plugin.client.Self));
            player.Team = team;

            var playerUpdate = new Event
            {
                user_updated_event = new Event.UserUpdatedEvent
                {
                    User = player
                }
            };
            Plugin.client.Send(new Packet
            {
                Event = playerUpdate
            });

            Destroy(_teamSelection.screen.gameObject);
        }

        private void SongSelection_SongSelected(GameplayParameters parameters) => SongSelection_SongSelected(parameters.Beatmap.LevelId);

        private async void SongSelection_SongSelected(string levelId)
        {
            //Load the song, then display the detail info
            var loadedLevel = await SongUtils.LoadSong(levelId);
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
                //Send updated download status
                var player = Plugin.client.GetUserByGuid(Plugin.client.Self.Guid);
                player.DownloadState = User.DownloadStates.Downloaded;

                var playerUpdate = new Event
                {
                    user_updated_event = new Event.UserUpdatedEvent
                    {
                        User = player
                    }
                };

                _ = Plugin.client.Send(new Packet
                {
                    Event = playerUpdate
                });

                //We don't want to recieve this since it would cause an infinite song loading loop.
                //Our song is already loaded inherently since we're selecting it as the host
                _ = Plugin.client.Send(
                    Match.AssociatedUsers.Where(x => x != player.Guid && Plugin.client.GetUserByGuid(x).ClientType == User.ClientTypes.Player).Select(x => Guid.Parse(x)).ToArray(),
                    new Packet
                    {
                        Command = new Command
                        {
                            load_song = new Command.LoadSong
                            {
                                LevelId = loadedLevel.levelID
                            }
                        }
                    });
            }
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
                characteristic.Difficulties = beatmapSet.beatmapDifficulties.Select(x => (int)x).ToArray();
                characteristics.Add(characteristic);
            }

            matchLevel.Characteristics.AddRange(characteristics);
            Match.SelectedLevel = matchLevel;
            Match.SelectedCharacteristic = Match.SelectedLevel.Characteristics.First(x =>
                x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            Match.SelectedDifficulty = (int)beatmap.difficulty;

            if (isHost)
            {
                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
                Task.Run(() => Plugin.client.UpdateMatch(Match));
            }

            _standardLevelDetailView.SetField("_selectedDifficultyBeatmap", beatmap);
            _standardLevelDetailViewController.InvokeEvent("didChangeDifficultyBeatmapEvent", new object[2] { _standardLevelDetailViewController, beatmap });
        }

        private void SongDetail_didPressPlayButtonEvent(IBeatmapLevel _, BeatmapCharacteristicSO characteristic,
            BeatmapDifficulty difficulty)
        {
            Plugin.client.Send(Match.AssociatedUsers.Where(x => Plugin.client.GetUserByGuid(x).ClientType == User.ClientTypes.Player).Select(x => Guid.Parse(x)).ToArray(), new Packet
            {
                Command = new Command
                {
                    play_song = new Command.PlaySong
                    {
                        GameplayParameters = new GameplayParameters
                        {
                            Beatmap = new Beatmap
                            {
                                Characteristic = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == characteristic.serializedName),
                                Difficulty = (int)difficulty,
                                LevelId = Match.SelectedLevel.LevelId
                            },
                            GameplayModifiers = new TournamentAssistantShared.Models.GameplayModifiers(),
                            PlayerSettings = new TournamentAssistantShared.Models.PlayerSpecificSettings()
                        },
                        FloatingScoreboard = true
                    }
                }
            });
        }

        protected override async Task UserInfoUpdated(User users)
        {
            await base.UserInfoUpdated(users);
        }

        protected override async Task MatchCreated(Match match)
        {
            await base.MatchCreated(match);

            if (TournamentMode && match.AssociatedUsers.Contains(Plugin.client.Self.Guid))
            {
                Match = match;

                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    //Player shouldn't be able to back out of a coordinated match
                    var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                    screenSystem.GetField<Button>("_backButton").interactable = false;

                    _splashScreen.StatusText = Plugin.GetLocalized("match_created_waiting_for_coordinator");
                });
            }
        }

        protected override async Task MatchInfoUpdated(Match match)
        {
            await base.MatchInfoUpdated(match);

            if (match.MatchEquals(Match))
            {
                Match = match;
                _playerList.Players = Plugin.client.State.Users.Where(x => match.AssociatedUsers.Contains(x.Guid) && x.ClientType == User.ClientTypes.Player).ToArray();

                bool isPlayer = Plugin.client.Self.ClientType == User.ClientTypes.Player;
                bool isAssociated = match.AssociatedUsers.Contains(Plugin.client.Self.Guid);
                if (isPlayer && isAssociated)
                {
                    var coordinators = match.AssociatedUsers
                        .SelectMany(guid => Plugin.client.State.Users.Where(u => u.Guid == guid))
                        .Where(x => x.ClientType == User.ClientTypes.Coordinator)
                        .ToList();
                    if (coordinators.Count == 0)
                    {
                        _ = SetBackButtonInteractivity(true);
                    }
                }

                if (!isHost && !match.AssociatedUsers.Contains(Plugin.client.Self.Guid))
                {
                    RemoveSelfFromMatch();
                }
                else if (!isHost && _songDetail && _songDetail.isInViewControllerHierarchy &&
                         match.SelectedLevel != null && match.SelectedCharacteristic != null)
                {
                    await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    {
                        //`CurrentlySelectedDifficulty` is reset by SetSelectedCharacteristic, so we save it here
                        //Usually this is intended behavior so that a new difficulty is selected
                        //when the new characteristic doesn't have a corresponding difficulty to the one
                        //that was previously selected. However... We don't want that here. Here, we
                        //know that the CurrentlySelectedDifficulty *should* be available on the new
                        //characteristic, if the coordinator/leader hasn't messed up, and often changes simultaneously
                        var selectedDifficulty = (int)match.SelectedDifficulty;

                        _songDetail.SetSelectedCharacteristic(match.SelectedCharacteristic.SerializedName);

                        if (match.SelectedCharacteristic.Difficulties.Contains(selectedDifficulty))
                        {
                            _songDetail.SetSelectedDifficulty(selectedDifficulty);
                        }
                    });
                }
            }
        }

        private Task SetBackButtonInteractivity(bool enable)
        {
            return UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.GetField<Button>("_backButton").interactable = enable;
            });
        }

        protected override async Task MatchDeleted(Match match)
        {
            await base.MatchDeleted(match);

            //If the match is destroyed while we're in here, back out
            if (match.MatchEquals(Match))
            {
                RemoveSelfFromMatch();
            }
        }

        private void RemoveSelfFromMatch()
        {
            if (Plugin.IsInMenu())
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    if (TournamentMode) SwitchToWaitingForCoordinatorMode();
                    else Dismiss();
                });
            }
            else
            {
                _ = SetBackButtonInteractivity(true);
                //If the player is in-game... boot them out... Yeah.
                //Harsh, but... Expected functionality
                //IN-TESTING: Temporarily disabled. Too many matches being accidentally ended by curious coordinators
                //PlayerUtils.ReturnToMenu();
            }
        }

        protected override async Task LoadedSong(IBeatmapLevel level)
        {
            await base.LoadedSong(level);

            if (Plugin.IsInMenu())
            {
                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    //If the player is still on the results screen, go ahead and boot them out
                    if (_resultsViewController.isInViewControllerHierarchy)
                    {
                        ResultsViewController_continueButtonPressedEvent(null);
                    }

                    SongSelection_SongSelected(level.levelID);
                });
            }
        }

        protected override async Task PlaySong(IPreviewBeatmapLevel desiredLevel,
            BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty,
            GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings,
            OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme,
            bool useFloatingScoreboard = false, bool useSync = false, bool disableFail = false,
            bool disablePause = false)
        {
            await base.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers,
                playerSpecificSettings, overrideEnvironmentSettings, colorScheme, useFloatingScoreboard, useSync,
                disableFail, disablePause);

            //Set up per-play settings
            Plugin.UseSync = useSync;
            Plugin.UseFloatingScoreboard = useFloatingScoreboard;
            Plugin.DisableFail = disableFail;
            Plugin.DisablePause = disablePause;

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                //If the player is still on the results screen, go ahead and boot them out
                if (_resultsViewController.isInViewControllerHierarchy) ResultsViewController_continueButtonPressedEvent(null);

                SongUtils.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSpecificSettings, SongFinished);
                if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);
            });
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = standardLevelScenesTransitionSetupData.difficultyBeatmap;
            var transformedMap = standardLevelScenesTransitionSetupData.transformedBeatmapData;
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

                var player = Plugin.client.GetUserByGuid(Plugin.client.Self.Guid);

                var songFinished = new Push.SongFinished
                {
                    Player = player,
                    Beatmap = new Beatmap
                    {
                        LevelId = map.level.levelID,
                        Difficulty = (int)map.difficulty,
                        Characteristic = new Characteristic
                        {
                            SerializedName = map.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName,
                            Difficulties = map.parentDifficultyBeatmapSet.difficultyBeatmaps.Select(x => (int)x.difficulty).ToArray()
                        }
                    },
                    Score = results.modifiedScore,
                    Misses = results.missedCount,
                    BadCuts = results.badCutsCount,
                    GoodCuts = results.goodCutsCount,
                    EndTime = results.endSongTime
                };

                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
                    songFinished.Type = Push.SongFinished.CompletionType.Passed;
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                    songFinished.Type = Push.SongFinished.CompletionType.Failed;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit)
                    songFinished.Type = Push.SongFinished.CompletionType.Quit;

                Plugin.client.Send(new Packet
                {
                    Push = new Push
                    {
                        song_finished = songFinished
                    }
                });
            }

            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Incomplete)
            {
                _menuLightsManager.SetColorPreset(_scoreLights, true);
                _resultsViewController.Init(results, transformedMap, map, false, highScore);
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