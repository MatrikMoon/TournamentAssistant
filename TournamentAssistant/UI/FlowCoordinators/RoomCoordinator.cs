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
    class RoomCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        public Match Match { get; set; }
        public CoreServer Server { get; set; }
        public PluginClient Client { get; set; }

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

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.TitleText = Plugin.GetLocalized("tournament_room");

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _playerList = BeatSaberUI.CreateViewController<PlayerList>();
            }

            if (addedToHierarchy)
            {
                Client.StateManager.MatchCreated += MatchCreated;
                Client.StateManager.MatchInfoUpdated += MatchUpdated;
                Client.StateManager.MatchDeleted += MatchDeleted;
                Client.LoadedSong += LoadedSong;
                Client.PlaySong += PlaySong;

                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");

                if (Client.StateManager.GetTournament(Client.SelectedTournament).Settings.EnableTeams)
                {
                    _teamSelection = BeatSaberUI.CreateViewController<TeamSelection>();
                    _teamSelection.TeamSelected += TeamSelection_TeamSelected;
                    _teamSelection.SetTeams(new List<Team>(Client.StateManager.GetTournament(Client.SelectedTournament).Settings.Teams));
                    ShowTeamSelection();
                }

                ProvideInitialViewControllers(_splashScreen);
            }
        }

        protected void SetBackButtonInteractivity(bool enable)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                screenSystem.GetField<Button>("_backButton").interactable = enable;
            });
        }

        public void DismissChildren()
        {
            if (_teamSelection?.screen) Destroy(_teamSelection.screen.gameObject);
            if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);

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

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetail) DismissViewController(topViewController);
            else if (!_songDetail.GetField<bool>("_isInTransition"))
            {
                DismissChildren();
                DidFinishEvent?.Invoke();
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

                DismissChildren();

                //Re-enable back button if it's disabled
                SetBackButtonInteractivity(true);

                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");
            }
        }

        private void TeamSelection_TeamSelected(Team team)
        {
            var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());
            player.Team = team;

            Task.Run(() => Client.UpdateUser(Client.SelectedTournament, player));

            //Destroy team selection screen
            Destroy(_teamSelection.screen.gameObject);
        }

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
                var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());
                player.DownloadState = User.DownloadStates.Downloaded;

                await Client.UpdateUser(Client.SelectedTournament, player);

                //We don't want to recieve this since it would cause an infinite song loading loop.
                //Our song is already loaded inherently since we're selecting it as the host
                var recipients = Match.AssociatedUsers
                    .Where(x => x != player.Guid && Client.StateManager.GetUser(Client.SelectedTournament, x).ClientType == User.ClientTypes.Player)
                    .ToArray();
                await Client.SendLoadSong(recipients, loadedLevel.levelID);
            }
        }

        protected async Task MatchCreated(Match match)
        {
            if (match.AssociatedUsers.Contains(Client.StateManager.GetSelfGuid()))
            {
                Match = match;

                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    //Player shouldn't be able to back out of a coordinated match
                    SetBackButtonInteractivity(false);

                    _splashScreen.StatusText = Plugin.GetLocalized("match_created_waiting_for_coordinator");
                });
            }
        }

        protected async Task MatchUpdated(Match match)
        {
            if (match.MatchEquals(Match))
            {
                Match = match;
                _playerList.Players = Client.StateManager
                    .GetUsers(Client.SelectedTournament)
                    .Where(x => match.AssociatedUsers.Contains(x.Guid) && x.ClientType == User.ClientTypes.Player)
                    .ToArray();

                //If there are no coordinators (or overlays, I suppose) connected to the match still,
                //reenable the back button
                var coordinators = match.AssociatedUsers
                        .Select(x => Client.StateManager.GetUser(Client.SelectedTournament, x))
                        .Where(x => x.ClientType != User.ClientTypes.Player)
                        .ToList();
                if (coordinators.Count <= 0)
                {
                    SetBackButtonInteractivity(true);
                }

                if (!isHost && !match.AssociatedUsers.Contains(Client.StateManager.GetSelfGuid()))
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

        protected Task MatchDeleted(Match match)
        {
            //If the match is destroyed while we're in here, back out
            if (match.MatchEquals(Match))
            {
                RemoveSelfFromMatch();
            }
            return Task.CompletedTask;
        }

        private void RemoveSelfFromMatch()
        {
            if (Plugin.IsInMenu())
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    SwitchToWaitingForCoordinatorMode();
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

        protected async Task LoadedSong(IBeatmapLevel level)
        {
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

        protected async Task PlaySong(IPreviewBeatmapLevel desiredLevel,
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

            //Send final score to server
            if (Client.Connected)
            {
                Logger.Debug($"SENDING RESULTS: {results.modifiedScore}");

                var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());

                var characteristic = new Characteristic
                {
                    SerializedName = map.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName,
                    Difficulties = map.parentDifficultyBeatmapSet.difficultyBeatmaps.Select(x => (int)x.difficulty).ToArray()
                };

                var type = Push.SongFinished.CompletionType.Quit;

                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) type = Push.SongFinished.CompletionType.Passed;
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed) type = Push.SongFinished.CompletionType.Failed;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit) type = Push.SongFinished.CompletionType.Quit;

                Client.SendSongFinished(player, map.level.levelID, (int)map.difficulty, characteristic, type, results.modifiedScore);
            }

            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Incomplete)
            {
                _menuLightsManager.SetColorPreset(_scoreLights, true);
                _resultsViewController.Init(results, transformedMap, map, false, highScore);
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(false);
                _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                PresentViewController(_resultsViewController, immediately: true);
            }
            else if (!Client.StateManager.GetMatches(Client.SelectedTournament).ContainsMatch(Match))
            {
                SwitchToWaitingForCoordinatorMode();
            }
        }

        private void ResultsViewController_continueButtonPressedEvent(ResultsViewController _)
        {
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);

            // If the match was destroyed while the player was in game, go back to waiting for cooridnator mode
            if (!Client.StateManager.GetMatches(Client.SelectedTournament).ContainsMatch(Match))
            {
                SwitchToWaitingForCoordinatorMode();
            }
        }

        //Broken off so that if custom notes isn't installed, we don't try to load anything from it
        private static void DisableHMDOnly()
        {
            CustomNotesInterop.DisableHMDOnly();
        }
    }
}