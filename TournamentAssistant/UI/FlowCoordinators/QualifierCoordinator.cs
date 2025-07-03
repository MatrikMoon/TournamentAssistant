#pragma warning disable IDE0052
using BeatSaberMarkupLanguage;
using BS_Utils.Gameplay;
using HMUI;
using IPA.Utilities.Async;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using UnityEngine.UI;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class QualifierCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        public QualifierEvent Event { get; set; }
        public CoreServer Server { get; set; }
        public PluginClient Client { get; set; }

        private SongSelection _songSelection;
        private SongDetail _songDetail;
        private RemainingAttempts _bottomText;

        private bool IsPractice { get; set; } = false;

        private Map _currentMap;
        private BeatmapLevel _lastPlayedBeatmapLevel;
        private BeatmapKey? _lastPlayedKey;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;

        private GameplaySetupViewController _gameplaySetupViewController;
        private GameplayModifiersPanelController _gameplayModifiersPanelController;

        private CustomLeaderboard _customLeaderboard;
        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _redLights;
        private MenuLightsPresetSO _defaultLights;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle(Plugin.GetLocalized("qualifier_room"), ViewController.AnimationType.None);
                showBackButton = true;

                _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _menuLightsManager = Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _resultsViewController = Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _scoreLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsClearedLightsPreset");
                _redLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsFailedLightsPreset");
                _defaultLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

                _songSelection = BeatSaberUI.CreateViewController<SongSelection>();
                _songSelection.SongSelected += SongSelection_SongSelected;

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _songDetail.PlayPressed += SongDetail_didPressPlayButtonEvent;
                _songDetail.PracticePressed += SongDetail_didPressPracticeButtonEvent;
                _songDetail.DisableCharacteristicControl = true;
                _songDetail.DisableDifficultyControl = true;
                _songDetail.DisablePlayButton = false;

                _customLeaderboard = BeatSaberUI.CreateViewController<CustomLeaderboard>();

                _gameplaySetupViewController = Resources.FindObjectsOfTypeAll<GameplaySetupViewController>().First();
                _gameplayModifiersPanelController = Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().First();
            }
            if (addedToHierarchy)
            {
                _songSelection.SetSongs(Event.QualifierMaps);
                ProvideInitialViewControllers(_songSelection);
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

        // Things that need to be done before every play of a map. ie: burn an attempt, enable per-map TA plugin settings
        private void PrePlaySetup()
        {
            Plugin.PreviousPlayState = User.PlayStates.InMenu;

            // Disable scores if we need to
            if (Event.Flags.HasFlag(QualifierEvent.EventSettings.DisableScoresaberSubmission))
            {
                ScoreSubmission.DisableSubmission(Constants.NAME);
            }

            // Enable anti-pause if we need to
            if (!IsPractice && _currentMap.GameplayParameters.DisablePause)
            {
                Plugin.QualifierDisablePause = true;
            }

            // If limited attempts are enabled for this song, be sure to burn an attempt on song start
            if (!IsPractice && _currentMap.GameplayParameters.Attempts > 0)
            {
                Task.Run(InitiateAttempt);
            }
        }

        private void SongDetail_didPressPracticeButtonEvent(BeatmapKey key, BeatmapLevel level)
        {
            IsPractice = true;
            PlaySong(key, level);
        }

        private void SongDetail_didPressPlayButtonEvent(BeatmapKey key, BeatmapLevel level)
        {
            IsPractice = false;
            PlaySong(key, level);
        }

        private void PlaySong(BeatmapKey key, BeatmapLevel level)
        {
            _lastPlayedBeatmapLevel = level;
            _lastPlayedKey = key;

            var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First().playerData;
            var playerSettings = playerData.playerSpecificSettings;

            //Override defaults if we have forced options enabled
            if (_currentMap.GameplayParameters.PlayerSettings.Options != PlayerOptions.NoPlayerOptions)
            {
                playerSettings = new PlayerSpecificSettings(
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.LeftHanded),
                        _currentMap.GameplayParameters.PlayerSettings.PlayerHeight,
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoPlayerHeight),
                        _currentMap.GameplayParameters.PlayerSettings.SfxVolume,
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.ReduceDebris),
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoHud),
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoFailEffects),
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdvancedHud),
                        false, //_currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoRestart),
                        _currentMap.GameplayParameters.PlayerSettings.SaberTrailIntensity,
                        (NoteJumpDurationTypeSettings)_currentMap.GameplayParameters.PlayerSettings.note_jump_duration_type_settings,
                        _currentMap.GameplayParameters.PlayerSettings.NoteJumpFixedDuration,
                        _currentMap.GameplayParameters.PlayerSettings.NoteJumpStartBeatOffset,
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.HideNoteSpawnEffect),
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdaptiveSfx),
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.ArcsHapticFeedback),
                        (ArcVisibilityType)_currentMap.GameplayParameters.PlayerSettings.arc_visibility_type,
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects,
                        _currentMap.GameplayParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects,
                        0.7f
                    );
            }

            var songSpeed = GameplayModifiers.SongSpeed.Normal;
            if (_currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SlowSong)) songSpeed = GameplayModifiers.SongSpeed.Slower;
            if (_currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastSong)) songSpeed = GameplayModifiers.SongSpeed.Faster;
            if (_currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SuperFastSong)) songSpeed = GameplayModifiers.SongSpeed.SuperFast;

            var gameplayModifiers = new GameplayModifiers(
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.BatteryEnergy) ? GameplayModifiers.EnergyType.Battery : GameplayModifiers.EnergyType.Bar,
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoFail),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.InstaFail),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FailOnClash),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoObstacles) ? GameplayModifiers.EnabledObstacleType.NoObstacles : GameplayModifiers.EnabledObstacleType.All,
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoBombs),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastNotes),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.StrictAngles),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.DisappearingArrows),
                songSpeed,
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoArrows),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.GhostNotes),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.ProMode) || playerData.gameplayModifiers.proMode, // Allow players to override promode setting
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.ZenMode),
                _currentMap.GameplayParameters.GameplayModifiers.Options.HasFlag(GameOptions.SmallCubes)
            );

            var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

            PrePlaySetup();

            SongUtils.PlaySong(key, playerData.overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSettings, SongFinished, SongRestarted);
        }

        private void SongSelection_SongSelected(Map map)
        {
            _currentMap = map;

            var loadedLevel = SongUtils.masterLevelList.FirstOrDefault(x => x.levelID == map.GameplayParameters.Beatmap.LevelId);

            PresentViewController(_songDetail, () =>
            {
                _songDetail.SetSelectedSong(loadedLevel);
                _songDetail.SetSelectedCharacteristic(map.GameplayParameters.Beatmap.Characteristic.SerializedName);
                _songDetail.SetSelectedDifficulty(map.GameplayParameters.Beatmap.Difficulty);

                _gameplaySetupViewController.Setup(true, true, true, false, PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);
                SetLeftScreenViewController(_gameplaySetupViewController, ViewController.AnimationType.In);

                DisableDisallowedModifierToggles(_gameplayModifiersPanelController);

                SetRightScreenViewController(_customLeaderboard, ViewController.AnimationType.In);

                if (_currentMap.GameplayParameters.Attempts > 0)
                {
                    // Disable play button until we get info about remaining attempts
                    _songDetail.DisablePlayButton = true;

                    _bottomText = BeatSaberUI.CreateViewController<RemainingAttempts>();
                    SetBottomScreenViewController(_bottomText, ViewController.AnimationType.In);
                }

                // TODO: Review whether this could cause issues. Probably need debouncing or something similar
                Task.Run(RequestLeaderboardAndAttempts);
            });
        }

        private void ResultsViewController_continueButtonPressedEvent(ResultsViewController results)
        {
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);
        }

        private void ResultsViewController_restartButtonPressedEvent(ResultsViewController results)
        {
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController, finishedCallback: () => PlaySong(_lastPlayedKey.Value, _lastPlayedBeatmapLevel));
        }

        public void SongRestarted(LevelScenesTransitionSetupDataSO levelScenesTransitionSetupData, LevelCompletionResults results)
        {
            PrePlaySetup();
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
            var maxPossibleMultipliedScore = ScoreModel.ComputeMaxMultipliedScoreForBeatmap(transformedMap);
            var modifierMultiplier = results.multipliedScore == 0 ? 0 : ((double)results.modifiedScore / results.multipliedScore);
            var maxPossibleModifiedScore = maxPossibleMultipliedScore * modifierMultiplier;

            // If NoFail is on, submit scores always, otherwise only submit when passed
            if (!IsPractice && results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared || (results.gameplayModifiers.noFailOn0Energy && results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed))
            {
                Task.Run(() => SubmitScore(results, (int)maxPossibleModifiedScore));
            }

            // Restart seems to be unused as of 1.29.1, in favor of the levelRestartedCallback in StartStandardLevel
            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Incomplete)
            {
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
                {
                    _menuLightsManager.SetColorPreset(_scoreLights, true);
                    _resultsViewController.Init(results, transformedMap, key, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += ResultsViewController_restartButtonPressedEvent;
                }
                else if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                {
                    _menuLightsManager.SetColorPreset(_redLights, true);
                    _resultsViewController.Init(results, transformedMap, key, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += ResultsViewController_restartButtonPressedEvent;
                }

                PresentViewController(_resultsViewController, immediately: true);
            }
        }

        private async Task InitiateAttempt()
        {
            var user = await GetUserInfo.GetUserAsync();

            // Disable the restart button until we know for sure another attempt can be made
            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _songDetail.DisablePlayButton = true;
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(false);
            });

            await Client.SendQualifierScore(Client.SelectedTournament, Event.Guid, _currentMap, user.platformUserId, user.userName, 0, 0, 0, 0, 0, 0, 0, 0, false, true);

            // If the player fails or quits, a score won't be submitted, so we should do this here
            var response = await Client.RequestAttempts(Client.SelectedTournament, Event.Guid, _currentMap.Guid);
            if (response.Type == Response.ResponseType.Success)
            {
                var remainingAttempts = response.remaining_attempts.remaining_attempts;
                Plugin.DisableRestart = remainingAttempts <= 0;

                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    _songDetail.DisablePlayButton = remainingAttempts <= 0;
                    _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(remainingAttempts > 0);
                    _bottomText.SetRemainingAttempts(remainingAttempts);
                });
            }
        }

        private async Task SubmitScore(LevelCompletionResults results, int maxPossibleScore)
        {
            var user = await GetUserInfo.GetUserAsync();
            var qualifierResponse = await Client.SendQualifierScore(Client.SelectedTournament, Event.Guid, _currentMap, user.platformUserId, user.userName, results.multipliedScore, results.modifiedScore, maxPossibleScore, maxPossibleScore == 0 ? 0 : ((double)results.modifiedScore / maxPossibleScore), results.missedCount, results.badCutsCount, results.goodCutsCount, results.maxCombo, results.fullCombo, false);
            if (qualifierResponse.Type == Response.ResponseType.Success)
            {
                var scores = qualifierResponse.leaderboard_entries.Scores.ToList();
                await UnityMainThreadTaskScheduler.Factory.StartNew(() => _customLeaderboard.SetScores(scores, Event.Sort, _currentMap.GameplayParameters.Target, user.platformUserId));
            }

            if (_currentMap.GameplayParameters.Attempts > 0)
            {
                var attemptResponse = await Client.RequestAttempts(Client.SelectedTournament, Event.Guid, _currentMap.Guid);
                if (attemptResponse.Type == Response.ResponseType.Success)
                {
                    var remainingAttempts = attemptResponse.remaining_attempts.remaining_attempts;
                    await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    {
                        _songDetail.DisablePlayButton = remainingAttempts <= 0;
                        _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(remainingAttempts > 0);
                        _bottomText.SetRemainingAttempts(remainingAttempts);
                    });
                }
            }
        }

        private async Task RequestLeaderboardAndAttempts()
        {
            var user = await GetUserInfo.GetUserAsync();

            var leaderboardResponse = await Client.RequestLeaderboard(Client.SelectedTournament, Event.Guid, _currentMap.Guid);
            if (leaderboardResponse.Type == Response.ResponseType.Success)
            {
                var scores = leaderboardResponse.leaderboard_entries.Scores.ToList();
                await UnityMainThreadTaskScheduler.Factory.StartNew(() => _customLeaderboard.SetScores(scores, Event.Sort, _currentMap.GameplayParameters.Target, user.platformUserId));
            }

            if (_currentMap.GameplayParameters.Attempts > 0)
            {
                var attemptResponse = await Client.RequestAttempts(Client.SelectedTournament, Event.Guid, _currentMap.Guid);
                if (attemptResponse.Type == Response.ResponseType.Success)
                {
                    var remainingAttempts = attemptResponse.remaining_attempts.remaining_attempts;
                    await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    {
                        _songDetail.DisablePlayButton = remainingAttempts <= 0;
                        _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(remainingAttempts > 0);
                        _bottomText.SetRemainingAttempts(remainingAttempts);
                    });
                }
            }
        }

        // Returns true if one of them was dismissed
        public bool DismissResultsOrDetailController()
        {
            if (topViewController is ResultsViewController)
            {
                _menuLightsManager.SetColorPreset(_defaultLights, false);
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                ReenableDisallowedModifierToggles(_gameplayModifiersPanelController);
                DismissViewController(_resultsViewController);
                return true;
            }
            else if (topViewController is SongDetail)
            {
                SetLeftScreenViewController(null, ViewController.AnimationType.Out);
                SetRightScreenViewController(null, ViewController.AnimationType.Out);
                SetBottomScreenViewController(null, ViewController.AnimationType.Out);
                DismissViewController(_songDetail);
                return true;
            }

            return false;
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (!DismissResultsOrDetailController())
            {
                DismissChildren();
                DidFinishEvent?.Invoke();
            }
        }

        public void DismissChildren()
        {
            while (topViewController is not SongSelection)
            {
                DismissViewController(topViewController, immediately: true);
            }

            // If the coordinator is dismissed from the outside (ie: on server disconnect)
            // we need to return all these things to normal
            _menuLightsManager.SetColorPreset(_defaultLights, false);
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _resultsViewController.restartButtonPressedEvent -= ResultsViewController_restartButtonPressedEvent;
            ReenableDisallowedModifierToggles(_gameplayModifiersPanelController);

            Plugin.DisableRestart = false;
        }
    }
}
