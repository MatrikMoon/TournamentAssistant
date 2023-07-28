using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using IPA.Utilities;
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
                _splashScreen.TitleText = Plugin.GetLocalized("tournament_room");

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _songDetail.PlayPressed += SongDetail_didPressPlayButtonEvent;
                _songDetail.DifficultyBeatmapChanged += SongDetail_didChangeDifficultyBeatmapEvent;

                _playerList = BeatSaberUI.CreateViewController<PlayerList>();
            }

            if (addedToHierarchy)
            {
                TournamentMode = Match == null;
                if (TournamentMode && Server != null)
                {
                    _splashScreen.StatusText = $"{Plugin.GetLocalized("connecting_to")} \"{Server.Name}\"...";
                    ProvideInitialViewControllers(_splashScreen);
                }
                else
                {
                    //If we're not in tournament mode, then a client connection has already been made
                    //by the room selection screen, so we can just assume Plugin.client isn't null
                    //NOTE: This is *such* a hack. Oh my god.
                    isHost = Match.Leader == Plugin.client.StateManager.GetSelfGuid();
                    _songSelection.SetSongs(SongUtils.masterLevelList);
                    _playerList.Players = Plugin.client.StateManager.GetUsers(TournamentId).Where(x => Match.AssociatedUsers.Contains(x.Guid) && x.ClientType == User.ClientTypes.Player).ToArray();
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
            DismissExtraViewControllers();
            base.Dismiss();
        }

        protected override async Task ConnectedToServer(Response.Connect response)
        {
            await base.ConnectedToServer(response);
        }

        protected override async Task FailedToConnectToServer(Response.Connect response)
        {
            await base.FailedToConnectToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Message)
                    ? response.Message
                    : Plugin.GetLocalized("failed_initial_attempt");
            });
        }

        protected override async Task JoinedTournament(Response.Join response)
        {
            await base.JoinedTournament(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");

                if (Plugin.client.StateManager.GetTournament(TournamentId).Settings.EnableTeams)
                {
                    _teamSelection = BeatSaberUI.CreateViewController<TeamSelection>();
                    _teamSelection.TeamSelected += TeamSelection_TeamSelected;
                    _teamSelection.SetTeams(new List<Team>(Plugin.client.StateManager.GetTournament(TournamentId).Settings.Teams));
                    ShowTeamSelection();
                }
            });
        }

        protected override async Task FailedToJoinTournament(Response.Join response)
        {
            await base.FailedToJoinTournament(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Message)
                    ? response.Message
                    : Plugin.GetLocalized("failed_initial_attempt");
            });
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetail) DismissViewController(topViewController);
            else if (!_songDetail.GetField<bool>("_isInTransition"))
            {
                if (!TournamentMode)
                {
                    if (isHost) Plugin.client?.DeleteMatch(TournamentId, Match);
                    else
                    {
                        Match.AssociatedUsers.Remove(Plugin.client.StateManager.GetSelfGuid());
                        Plugin.client?.UpdateMatch(TournamentId, Match);
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

        protected override async Task ShowModal(Request.ShowModal msg)
        {
            await base.ShowModal(msg);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
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

                DismissExtraViewControllers();

                //Re-enable back button if it's disabled
                SetBackButtonInteractivity(true);

                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");
            }
        }

        private void DismissExtraViewControllers()
        {
            //The results view and detail view aren't my own, they're the *real* views used in the
            //base game. As such, we should give them back them when we leave
            if (_resultsViewController.isInViewControllerHierarchy)
            {
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                _menuLightsManager.SetColorPreset(_defaultLights, false);
                DismissViewController(_resultsViewController, immediately: true);
            }

            if (_songDetail.isInViewControllerHierarchy) DismissViewController(_songDetail, immediately: true);
        }

        private void ModalResponse(ModalOption response, string modalId)
        {
            //Send response to coordinator or overlays associated with the match
            if (response != null)
            {
                var recipients = Match.AssociatedUsers
                    .Where(x => Plugin.client.StateManager.GetUser(TournamentId, x).ClientType != User.ClientTypes.Player)
                    .Select(Guid.Parse)
                    .ToArray();
                Plugin.client.RespondToModal(recipients, modalId, response);
            }

            //Destroy the modal
            if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);
            _serverMessage.OptionSelected -= ModalResponse;
            _serverMessage = null;
        }

        private void TeamSelection_TeamSelected(Team team)
        {
            var player = Plugin.client.StateManager.GetUser(TournamentId, Plugin.client.StateManager.GetSelfGuid());
            player.Team = team;

            Task.Run(() => Plugin.client.UpdateUser(TournamentId, player));

            //Destroy team selection screen
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
                var player = Plugin.client.StateManager.GetUser(TournamentId, Plugin.client.StateManager.GetSelfGuid());
                player.DownloadState = User.DownloadStates.Downloaded;

                await Plugin.client.UpdateUser(TournamentId, player);

                //We don't want to recieve this since it would cause an infinite song loading loop.
                //Our song is already loaded inherently since we're selecting it as the host
                var recipients = Match.AssociatedUsers
                    .Where(x => x != player.Guid && Plugin.client.StateManager.GetUser(TournamentId, x).ClientType == User.ClientTypes.Player)
                    .Select(Guid.Parse)
                    .ToArray();
                await Plugin.client.SendLoadSong(recipients, loadedLevel.levelID);
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
                var characteristic = new Characteristic
                {
                    SerializedName = beatmapSet.beatmapCharacteristic.serializedName,
                    Difficulties = beatmapSet.beatmapDifficulties.Select(x => (int)x).ToArray()
                };
                characteristics.Add(characteristic);
            }

            matchLevel.Characteristics.AddRange(characteristics);
            Match.SelectedLevel = matchLevel;
            Match.SelectedCharacteristic = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName);
            Match.SelectedDifficulty = (int)beatmap.difficulty;

            if (isHost)
            {
                //As of the async refactoring, this *shouldn't* cause problems to not await. It would be very hard to properly use async from a UI event so I'm leaving it like this for now
                Task.Run(() => Plugin.client.UpdateMatch(TournamentId, Match));
            }

            _standardLevelDetailView.SetField("_selectedDifficultyBeatmap", beatmap);
            _standardLevelDetailViewController.InvokeEvent("didChangeDifficultyBeatmapEvent", new object[2] { _standardLevelDetailViewController, beatmap });
        }

        private void SongDetail_didPressPlayButtonEvent(IBeatmapLevel _, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            var recipients = Match.AssociatedUsers.Where(x => Plugin.client.StateManager.GetUser(TournamentId, x).ClientType == User.ClientTypes.Player).Select(Guid.Parse).ToArray();
            var characteristicModel = Match.SelectedLevel.Characteristics.First(x => x.SerializedName == characteristic.serializedName);

            Plugin.client.SendPlaySong(recipients, Match.SelectedLevel.LevelId, characteristicModel, (int)difficulty);
        }

        protected override async Task UserInfoUpdated(User users)
        {
            await base.UserInfoUpdated(users);
        }

        protected override async Task MatchCreated(Match match)
        {
            await base.MatchCreated(match);

            if (TournamentMode && match.AssociatedUsers.Contains(Plugin.client.StateManager.GetSelfGuid()))
            {
                Match = match;

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //Player shouldn't be able to back out of a coordinated match
                    SetBackButtonInteractivity(false);

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
                _playerList.Players = Plugin.client.StateManager
                    .GetUsers(TournamentId)
                    .Where(x => match.AssociatedUsers.Contains(x.Guid) && x.ClientType == User.ClientTypes.Player)
                    .ToArray();

                //If there are no coordinators (or overlays, I suppose) connected to the match still,
                //reenable the back button
                var coordinators = match.AssociatedUsers
                        .Select(x => Plugin.client.StateManager.GetUser(TournamentId, x))
                        .Where(x => x.ClientType != User.ClientTypes.Player)
                        .ToList();
                if (coordinators.Count <= 0)
                {
                    SetBackButtonInteractivity(true);
                }

                if (!isHost && !match.AssociatedUsers.Contains(Plugin.client.StateManager.GetSelfGuid()))
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
                        var selectedDifficulty = match.SelectedDifficulty;

                        _songDetail.SetSelectedCharacteristic(match.SelectedCharacteristic.SerializedName);

                        if (match.SelectedCharacteristic.Difficulties.Contains(selectedDifficulty))
                        {
                            _songDetail.SetSelectedDifficulty(selectedDifficulty);
                        }
                    });
                }
            }
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
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (TournamentMode) SwitchToWaitingForCoordinatorMode();
                    else Dismiss();
                });
            }
            else
            {
                SetBackButtonInteractivity(true);
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
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //If the player is still on the results screen, go ahead and boot them out
                    if (_resultsViewController.isInViewControllerHierarchy)
                        ResultsViewController_continueButtonPressedEvent(null);

                    SongSelection_SongSelected(level.levelID);
                });
            }
        }

        protected override async Task PlaySong(IPreviewBeatmapLevel desiredLevel,
            BeatmapCharacteristicSO desiredCharacteristic,
            BeatmapDifficulty desiredDifficulty,
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            OverrideEnvironmentSettings overrideEnvironmentSettings,
            ColorScheme colorScheme,
            bool useFloatingScoreboard = false,
            bool useSync = false,
            bool disableFail = false,
            bool disablePause = false)
        {
            await base.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, gameplayModifiers, playerSpecificSettings, overrideEnvironmentSettings, colorScheme, useFloatingScoreboard, useSync, disableFail, disablePause);

            //Set up per-play settings
            Plugin.UseSync = useSync;
            Plugin.UseFloatingScoreboard = useFloatingScoreboard;
            Plugin.DisableFail = disableFail;
            Plugin.DisablePause = disablePause;

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
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

            //Send final score to server
            if (Plugin.client.Connected)
            {
                Logger.Debug($"SENDING RESULTS: {results.modifiedScore}");

                var player = Plugin.client.StateManager.GetUser(TournamentId, Plugin.client.StateManager.GetSelfGuid());

                var characteristic = new Characteristic
                {
                    SerializedName = map.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName,
                    Difficulties = map.parentDifficultyBeatmapSet.difficultyBeatmaps.Select(x => (int)x.difficulty).ToArray()
                };

                var type = Push.SongFinished.CompletionType.Quit;

                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) type = Push.SongFinished.CompletionType.Passed;
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed) type = Push.SongFinished.CompletionType.Failed;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit) type = Push.SongFinished.CompletionType.Quit;

                Plugin.client.SendSongFinished(player, map.level.levelID, (int)map.difficulty, characteristic, type, results.modifiedScore);
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
            else if (!Plugin.client.StateManager.GetMatches(TournamentId).ContainsMatch(Match))
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
            else if (!Plugin.client.StateManager.GetMatches(TournamentId).ContainsMatch(Match))
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