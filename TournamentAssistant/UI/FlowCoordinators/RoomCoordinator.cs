using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using IPA.Utilities.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TournamentAssistant.Interop;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.UnityUtilities;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Logger = TournamentAssistantShared.Logger;
using Match = TournamentAssistantShared.Models.Match;
using Team = TournamentAssistantShared.Models.Tournament.TournamentSettings.Team;
using Timer = System.Timers.Timer;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class RoomCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        private bool _didDisplayModifiersYet = false;

        public Match Match { get; set; }
        public CoreServer Server { get; set; }
        public PluginClient Client { get; set; }

        private SplashScreen _splashScreen;
        private PlayerList _playerList;
        private SongDetail _songDetail;

        private TeamSelection _teamSelection;
        private Timer _promptTimer;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private GameplaySetupViewController _gameplaySetupViewController;
        private GameplayModifiersPanelController _gameplayModifiersPanelController;

        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _defaultLights;
        private GameplayModifiersModelSO _gameplayModifiersModelSO;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                // Set up UI
                SetTitle(Plugin.GetLocalized("game_room"), ViewController.AnimationType.None);
                showBackButton = true;

                _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _menuLightsManager = Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _resultsViewController = Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _scoreLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsClearedLightsPreset");
                _defaultLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.TitleText = Plugin.GetLocalized("tournament_room");

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _playerList = BeatSaberUI.CreateViewController<PlayerList>();

                _gameplaySetupViewController = Resources.FindObjectsOfTypeAll<GameplaySetupViewController>().First();
                _gameplayModifiersPanelController = Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().First();
                _gameplayModifiersModelSO = _gameplayModifiersPanelController.GetField<GameplayModifiersModelSO>("_gameplayModifiersModel");
            }

            if (addedToHierarchy)
            {
                Client.StateManager.MatchCreated += MatchCreated;
                Client.StateManager.MatchInfoUpdated += MatchUpdated;
                Client.StateManager.MatchDeleted += MatchDeleted;
                Client.DownloadedSong += LoadedSong;
                Client.PlaySong += PlaySong;
                Client.ShowPrompt += ShowPrompt;

                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");

                if (Client.StateManager.GetTournament(Client.SelectedTournament).Settings.EnableTeams)
                {
                    _teamSelection = BeatSaberUI.CreateViewController<TeamSelection>();
                    _teamSelection.TeamSelected += TeamSelection_TeamSelected;
                    _teamSelection.SetTeams(new List<Team>(Client.StateManager.GetTournament(Client.SelectedTournament).Settings.Teams));
                    ShowTeamSelection();
                }

                // Mark the player as in the Tournament lobby
                var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());
                player.PlayState = User.PlayStates.WaitingForCoordinator;
                Task.Run(() => Client.UpdateUser(Client.SelectedTournament, player));

                // Set flag to display modifiers controller when transition is done
                _didDisplayModifiersYet = false;

                ProvideInitialViewControllers(_splashScreen);
            }
        }

        private async Task ShowPrompt(string fromPacketId, string fromUserId, Request.ShowPrompt prompt)
        {
            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                // If there's a transition going on (likely the results screen loading), we'll wait for it to finish
                // TODO: not sure this actually does anything, and it is gross. Investigate and remove if necessary
                if (this.GetField<bool>("_isInTransition", typeof(FlowCoordinator)))
                {
                    Logger.Warning("Showing while in transition");
                }

                var promptController = BeatSaberUI.CreateViewController<Prompt>();
                promptController.SetStartingInfo(fromPacketId, fromUserId, Client, prompt);
                promptController.ButtonPressed += (value) =>
                {
                    if (promptController.isInViewControllerHierarchy && !this.GetField<bool>("_isInTransition", typeof(FlowCoordinator)))
                    {
                        DismissViewController(promptController, immediately: true);
                    }
                };

                PresentViewController(promptController, immediately: true);

                // If a timer was involved, we should automatically dismiss when the timer expires
                if (prompt.ShowTimer)
                {
                    _promptTimer = new Timer(1000 * prompt.Timeout);
                    _promptTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
                    {
                        UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                        {
                            Logger.Warning("Timer dismissing");

                            // If there's a transition going on (likely the results screen loading), we'll wait for it to finish
                            // TODO: not sure this actually does anything, and it is gross. Investigate and remove if necessary
                            if (this.GetField<bool>("_isInTransition", typeof(FlowCoordinator)))
                            {
                                Logger.Warning("Timer dismissing while in transition");
                            }

                            if (promptController.isInViewControllerHierarchy && !this.GetField<bool>("_isInTransition", typeof(FlowCoordinator)))
                            {
                                DismissViewController(promptController, immediately: true);
                            }
                        });
                    };
                    _promptTimer.AutoReset = false;
                    _promptTimer.Enabled = true;
                }
            });

            // If there was no timer, we should send a response immediately
            if (!prompt.ShowTimer)
            {
                await Client.SendPromptResopnse(fromPacketId, fromUserId, "");
            }
        }

        protected override void TransitionDidFinish()
        {
            base.TransitionDidFinish();

            if (!_didDisplayModifiersYet)
            {
                _didDisplayModifiersYet = true;
                _gameplaySetupViewController.Setup(true, true, true, false, PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);
                SetLeftScreenViewController(_gameplaySetupViewController, ViewController.AnimationType.In);
                DisableDisallowedModifierToggles(_gameplayModifiersPanelController);
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                // Mark the player as not in the Tournament lobby
                var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());
                player.PlayState = User.PlayStates.InMenu;
                Task.Run(() => Client.UpdateUser(Client.SelectedTournament, player));
            }
        }

        private void DisableDisallowedModifierToggles(GameplayModifiersPanelController controller)
        {
            var toggles = controller.GetField<GameplayModifierToggle[]>("_gameplayModifierToggles");
            var disallowedToggles = toggles.Where(x => x.name != "ProMode");

            foreach (var toggle in disallowedToggles)
            {
                toggle.gameObject.SetActive(false);
            }
        }

        private void ReenableDisallowedModifierToggles(GameplayModifiersPanelController controller)
        {
            var toggles = controller.GetField<GameplayModifierToggle[]>("_gameplayModifierToggles");

            if (toggles != null)
            {
                foreach (var toggle in toggles)
                {
                    toggle.gameObject.SetActive(true);
                }
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

        public void DismissChildren(bool dismissModifierPanel = true)
        {
            if (_teamSelection?.screen)
            {
                Destroy(_teamSelection.screen.gameObject);
            }

            // If any prompts are showing, dismiss them
            while (topViewController is Prompt)
            {
                DismissViewController(topViewController, immediately: true);
            }

            // The results view and detail view aren't my own, they're the *real* views used in the
            // base game. As such, we should give them back them when we leave
            if (_resultsViewController.isInViewControllerHierarchy)
            {
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                _resultsViewController.GetField<Button>("_continueButton").gameObject.SetActive(true);
                _menuLightsManager.SetColorPreset(_defaultLights, false);
                DismissViewController(_resultsViewController, immediately: true);
            }

            if (_songDetail.isInViewControllerHierarchy)
            {
                DismissViewController(_songDetail, immediately: true);
            }

            // Dismiss modifiers panel
            if (dismissModifierPanel)
            {
                SetLeftScreenViewController(null, ViewController.AnimationType.None);
                ReenableDisallowedModifierToggles(_gameplayModifiersPanelController);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SongDetail) DismissViewController(topViewController, immediately: true);
            else if (!this.GetField<bool>("_isInTransition", typeof(FlowCoordinator)))
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

                DismissChildren(false);

                // Re-enable back button if it's disabled
                SetBackButtonInteractivity(true);

                _splashScreen.StatusText = Plugin.GetLocalized("waiting_for_coordinator");
            }
        }

        private void TeamSelection_TeamSelected(Team team)
        {
            var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());
            player.TeamId = team.Guid;

            Task.Run(() => Client.UpdateUser(Client.SelectedTournament, player));

            // Destroy team selection screen
            Destroy(_teamSelection.screen.gameObject);
        }

        private async void SongSelection_SongSelected(string levelId)
        {
            var loadedLevel = SongUtils.masterLevelList.FirstOrDefault(x => x.levelID.ToUpper() == levelId.ToUpper());

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                // Load the song, then display the detail info
                if (!_songDetail.isInViewControllerHierarchy)
                {
                    PresentViewController(_songDetail, () =>
                    {
                        _songDetail.DisableCharacteristicControl = true;
                        _songDetail.DisableDifficultyControl = true;
                        _songDetail.DisablePlayButton = true;
                        _songDetail.SetSelectedSong(loadedLevel);
                        _songDetail.SetSelectedCharacteristic(Match.SelectedMap.GameplayParameters.Beatmap.Characteristic.SerializedName);
                        _songDetail.SetSelectedDifficulty(Match.SelectedMap.GameplayParameters.Beatmap.Difficulty);
                    }, immediately: true);
                }
                else
                {
                    _songDetail.DisableCharacteristicControl = true;
                    _songDetail.DisableDifficultyControl = true;
                    _songDetail.DisablePlayButton = true;
                    _songDetail.SetSelectedSong(loadedLevel);
                    _songDetail.SetSelectedCharacteristic(Match.SelectedMap.GameplayParameters.Beatmap.Characteristic.SerializedName);
                    _songDetail.SetSelectedDifficulty(Match.SelectedMap.GameplayParameters.Beatmap.Difficulty);
                }
            });
        }

        protected async Task MatchCreated(Match match)
        {
            if (match.AssociatedUsers.Contains(Client.StateManager.GetSelfGuid()) && Match == null)
            {
                Client.CurrentMatch = match.Guid;
                Match = match;

                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    // Player shouldn't be able to back out of a coordinated match
                    SetBackButtonInteractivity(false);

                    // Dismiss results screen if it was open
                    DismissChildren(false);

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
                // x != null was a stopgap that I'm not sure fixes anything
                var coordinators = match.AssociatedUsers
                        .Select(x => Client.StateManager.GetUser(Client.SelectedTournament, x))
                        .Where(x => x != null && x.ClientType != User.ClientTypes.Player)
                        .ToList();
                if (coordinators.Count <= 0)
                {
                    SetBackButtonInteractivity(true);
                }

                if (!match.AssociatedUsers.Contains(Client.StateManager.GetSelfGuid()))
                {
                    RemoveSelfFromMatch();
                }
                else if (_songDetail && _songDetail.isInViewControllerHierarchy &&
                         match.SelectedMap != null && match.SelectedMap.GameplayParameters.Beatmap.Characteristic != null)
                {
                    await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    {
                        //`CurrentlySelectedDifficulty` is reset by SetSelectedCharacteristic, so we save it here
                        //Usually this is intended behavior so that a new difficulty is selected
                        //when the new characteristic doesn't have a corresponding difficulty to the one
                        //that was previously selected. However... We don't want that here. Here, we
                        //know that the CurrentlySelectedDifficulty *should* be available on the new
                        //characteristic, if the coordinator/leader hasn't messed up, and often changes simultaneously
                        var selectedDifficulty = match.SelectedMap.GameplayParameters.Beatmap.Difficulty;

                        _songDetail.SetSelectedCharacteristic(match.SelectedMap.GameplayParameters.Beatmap.Characteristic.SerializedName);
                        _songDetail.SetSelectedDifficulty(selectedDifficulty);
                    });
                }
            }
        }

        protected Task MatchDeleted(Match match)
        {
            // If the match is destroyed while we're in here, back out
            if (match.MatchEquals(Match))
            {
                Match = null;
                Client.CurrentMatch = null;
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
                // If the player is in-game... boot them out... Yeah.
                // Harsh, but... Expected functionality
                // IN-TESTING: Temporarily disabled. Too many matches being accidentally ended by curious coordinators
                // PlayerUtils.ReturnToMenu();
            }
        }

        protected async Task LoadedSong(BeatmapLevel level)
        {
            if (Plugin.IsInMenu())
            {
                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    // If the player is still on the results screen, go ahead and boot them out
                    if (_resultsViewController.isInViewControllerHierarchy)
                    {
                        DismissResultsScreen();
                    }

                    // If any prompts are showing, dismiss them
                    while (topViewController is Prompt)
                    {
                        DismissViewController(topViewController, immediately: true);
                    }

                    SongSelection_SongSelected(level.levelID);
                });
            }
        }

        protected async Task PlaySong(BeatmapKey desiredKey,
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            OverrideEnvironmentSettings overrideEnvironmentSettings,
            ColorScheme colorScheme,
            bool useFloatingScoreboard = false,
            bool useSync = false,
            bool disableFail = false,
            bool disablePause = false)
        {
            // Set up per-play settings
            Plugin.UseSync = useSync;
            Plugin.UseFloatingScoreboard = useFloatingScoreboard;
            Plugin.DisableFail = disableFail;
            Plugin.DisablePause = disablePause;
            Plugin.PreviousPlayState = User.PlayStates.WaitingForCoordinator;

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                // If the player is still on the results screen, go ahead and boot them out
                if (_resultsViewController.isInViewControllerHierarchy)
                {
                    DismissResultsScreen();
                }

                // If any prompts are showing, dismiss them
                while (topViewController is Prompt)
                {
                    DismissViewController(topViewController, immediately: true);
                }

                SongUtils.PlaySong(desiredKey, overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSpecificSettings, SongFinished);
            });
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = standardLevelScenesTransitionSetupData.beatmapLevel;
            var key = standardLevelScenesTransitionSetupData.beatmapKey;
            var transformedMap = standardLevelScenesTransitionSetupData.transformedBeatmapData;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetOrCreatePlayerLevelStatsData(map.levelID, key.difficulty, key.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            // Compute max possible modified score given provided data
            var filterNoteTypesFromMaxScore = !MidPlayModifiers.NoteDisablingWasChangedMidPlay && (MidPlayModifiers.SongStartedWithoutBlueNotes ^ MidPlayModifiers.SongStartedWithoutRedNotes);
            var maxPossibleMultipliedScore = SongUtils.ComputeMaxMultipliedScoreForBeatmap(transformedMap, filterNoteTypesFromMaxScore, MidPlayModifiers.SongStartedWithoutBlueNotes ? ColorType.ColorB : ColorType.ColorA);
            var maxPossibleModifiedScore = _gameplayModifiersModelSO.MaxModifiedScoreForMaxMultipliedScore(maxPossibleMultipliedScore, _gameplayModifiersModelSO.CreateModifierParamsList(results.gameplayModifiers), 1f);

            // Not really necessary to clean up, but nice to do
            MidPlayModifiers.SongStartedWithoutBlueNotes = false;
            MidPlayModifiers.SongStartedWithoutRedNotes = false;
            MidPlayModifiers.NoteDisablingWasChangedMidPlay = false;

            // Disable HMD Only if it was enabled (or even if not. Doesn't matter to me)
            var customNotes = IPA.Loader.PluginManager.GetPluginFromId("CustomNotes");
            if (customNotes != null)
            {
                DisableHMDOnly();
            }

            // Send final score to server
            if (Client.Connected)
            {
                Logger.Debug($"SENDING RESULTS: {results.modifiedScore}");

                var player = Client.StateManager.GetUser(Client.SelectedTournament, Client.StateManager.GetSelfGuid());

                var characteristic = new Characteristic
                {
                    SerializedName = key.beatmapCharacteristic.serializedName,
                    Difficulties = map.GetDifficulties(key.beatmapCharacteristic).Select(x => (int)x).ToArray()
                };

                var type = Push.SongFinished.CompletionType.Quit;

                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) type = Push.SongFinished.CompletionType.Passed;
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed) type = Push.SongFinished.CompletionType.Failed;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit) type = Push.SongFinished.CompletionType.Quit;

                Client.SendSongFinished(Client.SelectedTournament, Match.Guid, player, map.levelID, (int)key.difficulty, characteristic, type, results.modifiedScore, results.missedCount, results.badCutsCount, results.goodCutsCount, results.endSongTime, maxPossibleModifiedScore, maxPossibleModifiedScore > 0 ? results.modifiedScore / (double)maxPossibleModifiedScore : 0);

                if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Incomplete)
                {
                    _menuLightsManager.SetColorPreset(_scoreLights, true);
                    _resultsViewController.Init(results, transformedMap, key, map, false, highScore);
                    _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(false);
                    _resultsViewController.GetField<Button>("_continueButton").gameObject.SetActive(false);
                    _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                    PresentViewController(_resultsViewController, immediately: true);
                }
                else
                {
                    // TODO: Can we kick them all the way out? Does it do that already?
                    // SwitchToWaitingForCoordinatorMode();
                }
            }
        }

        private void ResultsViewController_continueButtonPressedEvent(ResultsViewController _)
        {
            DismissResultsScreen();
        }

        private void DismissResultsScreen()
        {
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _resultsViewController.GetField<Button>("_continueButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLights, true);

            DismissViewController(_resultsViewController, ViewController.AnimationDirection.Horizontal, finishedCallback: () =>
            {
                // If the match was destroyed while the player was in game, go back to waiting for cooridnator mode
                if (!Client.StateManager.GetMatches(Client.SelectedTournament).ContainsMatch(Match))
                {
                    SwitchToWaitingForCoordinatorMode();
                }
            }, immediately: true);
        }

        // Broken off so that if custom notes isn't installed, we don't try to load anything from it
        private static void DisableHMDOnly()
        {
            CustomNotesInterop.DisableHMDOnly();
        }
    }
}