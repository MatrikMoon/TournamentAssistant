#pragma warning disable IDE0052
using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Interop;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class QualifierCoordinator : FlowCoordinator
    {
        public event Action DidFinishEvent;

        public QualifierEvent Event { get; set; }
        public CoreServer EventHost { get; set; }

        private SongSelection _songSelection;
        private SongDetail _songDetail;

        private GameplayParameters _currentParameters;
        private IBeatmapLevel _lastPlayedBeatmapLevel;
        private BeatmapCharacteristicSO _lastPlayedCharacteristic;
        private BeatmapDifficulty _lastPlayedDifficulty;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private CampaignFlowCoordinator _campaignFlowCoordinator;

        private GameplaySetupViewController _gameplaySetupViewController;
        private CustomLeaderboard _customLeaderboard;
        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _redLights;
        private MenuLightsPresetSO _defaultLights;

        private bool _inPlay;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle(Plugin.GetLocalized("qualifier_room"), ViewController.AnimationType.None);
                showBackButton = true;
                _inPlay = false;

                _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _menuLightsManager = Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _campaignFlowCoordinator = Resources.FindObjectsOfTypeAll<CampaignFlowCoordinator>().First();
                _resultsViewController = Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _scoreLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsClearedLightsPreset");
                _redLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsFailedLightsPreset");
                _defaultLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

                _songSelection = BeatSaberUI.CreateViewController<SongSelection>();
                _songSelection.SongSelected += SongSelection_SongSelected;

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _songDetail.PlayPressed += CheckAttemptsRemainingThenPlay;
                _songDetail.DisableCharacteristicControl = true;
                _songDetail.DisableDifficultyControl = true;
                _songDetail.DisablePlayButton = false;

                _customLeaderboard = BeatSaberUI.CreateViewController<CustomLeaderboard>();
            }
            if (addedToHierarchy)
            {
                _songSelection.SetSongs(Event.QualifierMaps.ToList());
                ProvideInitialViewControllers(_songSelection);
            }
        }

        private void CheckAttemptsRemainingThenPlay(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            if (!_inPlay)
            {
                _inPlay = true;
                Task.Run(async () =>
                {
                    await PlayerUtils.GetPlatformUserData(async (username, userId) =>
                    {
                        Logger.Warning("Checking remaining attempts...");

                        var remainingAttempts = await EUCInterop.CheckRemainingAttempts(Convert.ToString(userId), level.levelID, (int)difficulty);
                        if (remainingAttempts > 0)
                        {
                            UpdateUIWithRemainingAttempts(remainingAttempts);

                            //Initiate a score attempt
                            Logger.Warning("Creating score attempt...");

                            await EUCInterop.CreateScore(Convert.ToString(userId), level.levelID, (int)difficulty);

                            Logger.Warning("Score Created, Playing the song!");

                            //Play the song
                            UnityMainThreadDispatcher.Instance().Enqueue(() => SongDetail_didPressPlayButtonEvent(level, characteristic, difficulty));
                        }
                        else
                        {
                            //No more attempts!
                            Logger.Warning("No More attempts!");
                            _inPlay = false;
                        }
                    });
                });
            }
            else
            {
                Logger.Warning("Multiple attempts to play song.");
            }
        }

        private void CheckAttemptsRemainingAndUpdateUI(string levelId, int difficulty)
        {
            Task.Run(async () =>
            {
                await PlayerUtils.GetPlatformUserData(async (username, userId) =>
                {
                    Logger.Warning("Checking remaining attempts...");

                    var remainingAttempts = await EUCInterop.CheckRemainingAttempts(Convert.ToString(userId), levelId, difficulty);
                    UpdateUIWithRemainingAttempts(remainingAttempts);

                    Logger.Warning($"{remainingAttempts} attempts remaining");
                });
            });
        }

        private void UpdateUIWithRemainingAttempts(int remainingAttempts)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                FloatingInfoText.Instance.UpdateText($"Remaining Attempts: {remainingAttempts}");
            });
        }

        private void SendScoreToEUC(LevelCompletionResults results)
        {
            Task.Run(async () =>
            {
                await PlayerUtils.GetPlatformUserData(async (username, userId) =>
                {
                    await EUCInterop.SubmitScore(Convert.ToString(userId), results.multipliedScore);
                });
            });
        }

        private void SongDetail_didPressPlayButtonEvent(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            Plugin.DisablePause = true;

            _lastPlayedBeatmapLevel = level;
            _lastPlayedCharacteristic = characteristic;
            _lastPlayedDifficulty = difficulty;
            
            var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First().playerData;
            var playerSettings = playerData.playerSpecificSettings;

            //Override defaults if we have forced options enabled
            if (_currentParameters.PlayerSettings.Options != PlayerOptions.None)
            {
                playerSettings = new PlayerSpecificSettings(
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.LeftHanded),
                        _currentParameters.PlayerSettings.PlayerHeight,
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoPlayerHeight),
                        _currentParameters.PlayerSettings.SfxVolume,
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.ReduceDebris),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoHud),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoFailEffects),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdvancedHud),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AutoRestart),
                        _currentParameters.PlayerSettings.SaberTrailIntensity,
                        (NoteJumpDurationTypeSettings)_currentParameters.PlayerSettings.note_jump_duration_type_settings,
                        _currentParameters.PlayerSettings.NoteJumpFixedDuration,
                        _currentParameters.PlayerSettings.NoteJumpStartBeatOffset,
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.HideNoteSpawnEffect),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdaptiveSfx),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects,
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects
                    );
            }

            var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

            //Disable scores if we need to
            if (((QualifierEvent.EventSettings)Event.Flags).HasFlag(QualifierEvent.EventSettings.DisableScoresaberSubmission)) BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Constants.NAME);

            SongUtils.PlaySong(level, characteristic, difficulty, playerData.overrideEnvironmentSettings, colorScheme, playerData.gameplayModifiers, playerSettings, SongFinished);
        }

        private async void SongSelection_SongSelected(GameplayParameters parameters)
        {
            _currentParameters = parameters;

            var loadedLevel = await SongUtils.LoadSong(parameters.Beatmap.LevelId);
            PresentViewController(_songDetail, () =>
            {
                _songDetail.SetSelectedSong(loadedLevel);
                _songDetail.SetSelectedDifficulty((int)parameters.Beatmap.Difficulty);
                _songDetail.SetSelectedCharacteristic(parameters.Beatmap.Characteristic.SerializedName);

                new GameObject("FloatingScoreScreen").AddComponent<FloatingInfoText>();

                CheckAttemptsRemainingAndUpdateUI(loadedLevel.levelID, parameters.Beatmap.Difficulty);

                if (_gameplaySetupViewController == null)
                {
                    _gameplaySetupViewController = Resources.FindObjectsOfTypeAll<GameplaySetupViewController>().First();
                    _gameplaySetupViewController.name = "TA_GameplaySetup";
                    _gameplaySetupViewController.Setup(true, true, true, false, PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);
                }

                SetLeftScreenViewController(_gameplaySetupViewController, ViewController.AnimationType.In);

                //TODO: Review whether this could cause issues. Probably need debouncing or something similar
                Task.Run(() => PlayerUtils.GetPlatformUserData(RequestLeaderboardWhenResolved));
                SetRightScreenViewController(_customLeaderboard, ViewController.AnimationType.In);
            });
        }

        private void ResultsViewController_continueButtonPressedEvent(ResultsViewController results)
        {
            _inPlay = false;
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);
        }

        private void ResultsViewController_restartButtonPressedEvent(ResultsViewController results)
        {
            _inPlay = false;
            _resultsViewController.continueButtonPressedEvent -= ResultsViewController_continueButtonPressedEvent;
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController, finishedCallback: () => CheckAttemptsRemainingThenPlay(_lastPlayedBeatmapLevel, _lastPlayedCharacteristic, _lastPlayedDifficulty));
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = standardLevelScenesTransitionSetupData.difficultyBeatmap;
            var transformedMap = standardLevelScenesTransitionSetupData.transformedBeatmapData;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Restart) CheckAttemptsRemainingThenPlay(_lastPlayedBeatmapLevel, _lastPlayedCharacteristic, _lastPlayedDifficulty);
            else if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Incomplete)
            {
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
                {
                    Task.Run(() => PlayerUtils.GetPlatformUserData((username, userId) => SubmitScoreWhenResolved(username, userId, results)));

                    SendScoreToEUC(results);

                    _menuLightsManager.SetColorPreset(_scoreLights, true);
                    _resultsViewController.Init(results, transformedMap, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += ResultsViewController_restartButtonPressedEvent;
                }
                else if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                {
                    _menuLightsManager.SetColorPreset(_redLights, true);
                    _resultsViewController.Init(results, transformedMap, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += ResultsViewController_restartButtonPressedEvent;
                }

                PresentViewController(_resultsViewController, immediately: true);
            }
        }

        private Task SubmitScoreWhenResolved(string username, ulong userId, LevelCompletionResults results)
        {
            Task.Run(async () =>
            {
                var scores = (await HostScraper.RequestResponse(EventHost, new Packet
                {
                    Push = new Push
                    {
                        leaderboard_score = new Push.LeaderboardScore
                        {
                            Score = new LeaderboardScore
                            {
                                EventId = Event.Guid,
                                Parameters = _currentParameters,
                                UserId = userId.ToString(),
                                Username = username,
                                FullCombo = results.fullCombo,
                                Score = results.modifiedScore,
                                Color = "#ffffff"
                            }
                        }
                    }
                }, username, userId)).Response.leaderboard_scores.Scores.Take(10).ToArray();

                UnityMainThreadDispatcher.Instance().Enqueue(() => SetCustomLeaderboardScores(scores, userId));
            });
            return Task.CompletedTask;
        }

        private Task RequestLeaderboardWhenResolved(string username, ulong userId)
        {
            //Don't scrape on main thread
            Task.Run(async () =>
            {
                var scores = (await HostScraper.RequestResponse(EventHost, new Packet
                {
                    Request = new Request
                    {
                        leaderboard_score = new Request.LeaderboardScore
                        {
                            EventId = Event.Guid,
                            Parameters = _currentParameters
                        }
                    }
                }, username, userId)).Response.leaderboard_scores.Scores.Take(10).ToArray();

                UnityMainThreadDispatcher.Instance().Enqueue(() => SetCustomLeaderboardScores(scores, userId));
            });
            return Task.CompletedTask;
        }

        public void SetCustomLeaderboardScores(LeaderboardScore[] scores, ulong userId)
        {
            var place = 1;
            var indexOfme = -1;
            _customLeaderboard.SetScores(scores.Select(x =>
            {
                if (x.UserId == userId.ToString()) indexOfme = place - 1;
                return new LeaderboardTableView.ScoreData(x.Score, x.Username, place++, x.FullCombo);
            }).ToList(), indexOfme);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (_resultsViewController.isInViewControllerHierarchy)
            {
                _menuLightsManager.SetColorPreset(_defaultLights, false);
                DismissViewController(_resultsViewController);
            }
            else if (_songDetail.isInViewControllerHierarchy)
            {
                if (FloatingInfoText.Instance != null) FloatingInfoText.Destroy();

                SetLeftScreenViewController(null, ViewController.AnimationType.Out);
                SetRightScreenViewController(null, ViewController.AnimationType.Out);
                DismissViewController(_songDetail);
            }
            else DidFinishEvent?.Invoke();
        }
    }
}
