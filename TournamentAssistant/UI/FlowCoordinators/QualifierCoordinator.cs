using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;

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

        private PlatformLeaderboardViewController _globalLeaderboard;
        private CustomLeaderboard _customLeaderboard;
        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _redLights;
        private MenuLightsPresetSO _defaultLights;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                title = "Qualifier Room";
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

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                _songDetail.PlayPressed += songDetail_didPressPlayButtonEvent;
                _songDetail.DisableCharacteristicControl = true;
                _songDetail.DisableDifficultyControl= true;
                _songDetail.DisablePlayButton= false;
            }
            if (activationType == ActivationType.AddedToHierarchy)
            {
                _songSelection.SetSongs(Event.QualifierMaps.ToList());
                ProvideInitialViewControllers(_songSelection);
            }
        }

        private void songDetail_didPressPlayButtonEvent(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            _lastPlayedBeatmapLevel = level;
            _lastPlayedCharacteristic = characteristic;
            _lastPlayedDifficulty = difficulty;

            var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First().playerData;
            var playerSettings = playerData.playerSpecificSettings;

            //Override defaults if we have forced options enabled
            if (_currentParameters.PlayerSettings.Options != PlayerOptions.None)
            {
                playerSettings = new PlayerSpecificSettings();
                playerSettings.leftHanded = _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.LeftHanded);
                playerSettings.staticLights = _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights);
                playerSettings.noTextsAndHuds = _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.NoHud);
                playerSettings.advancedHud = _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdvancedHud);
                playerSettings.reduceDebris = _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.ReduceDebris);
            }

            var gameplayModifiers = new GameplayModifiers();
            gameplayModifiers.batteryEnergy = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.BatteryEnergy);
            gameplayModifiers.disappearingArrows = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.DisappearingArrows);
            gameplayModifiers.failOnSaberClash = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.FailOnClash);
            gameplayModifiers.fastNotes = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastNotes);
            gameplayModifiers.ghostNotes = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.GhostNotes);
            gameplayModifiers.instaFail = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.InstaFail);
            gameplayModifiers.noBombs = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoBombs);
            gameplayModifiers.noFail = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoFail);
            gameplayModifiers.noObstacles = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoObstacles);
            gameplayModifiers.noArrows = _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoArrows);

            if (_currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.SlowSong)) gameplayModifiers.songSpeed = GameplayModifiers.SongSpeed.Slower;
            if (_currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastSong)) gameplayModifiers.songSpeed = GameplayModifiers.SongSpeed.Faster;

            var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

            SongUtils.PlaySong(level, characteristic, difficulty, playerData.overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSettings, SongFinished);
        }

        private void songSelection_SongSelected(GameplayParameters parameters)
        {
            _currentParameters = parameters;

            SongUtils.LoadSong(parameters.Beatmap.LevelId, (loadedLevel) =>
            {
                PresentViewController(_songDetail, () =>
                {
                    _songDetail.SetSelectedSong(loadedLevel);
                    _songDetail.SetSelectedDifficulty((int)parameters.Beatmap.Difficulty);
                    _songDetail.SetSelectedCharacteristic(parameters.Beatmap.Characteristic.SerializedName);

                    if (_globalLeaderboard == null)
                    {
                        _globalLeaderboard = Resources.FindObjectsOfTypeAll<PlatformLeaderboardViewController>().First();
                        _globalLeaderboard.name = "Global Leaderboard";
                    }

                    _globalLeaderboard.SetData(SongUtils.GetClosestDifficultyPreferLower(loadedLevel, (BeatmapDifficulty)(int)parameters.Beatmap.Difficulty, parameters.Beatmap.Characteristic.SerializedName));
                    SetRightScreenViewController(_globalLeaderboard);

                    _customLeaderboard = BeatSaberUI.CreateViewController<CustomLeaderboard>();
                    PlayerUtils.GetPlatformUserData(RequestLeaderboardWhenResolved);
                    SetLeftScreenViewController(_customLeaderboard);
                });
            });
        }

        private void resultsViewController_continueButtonPressedEvent(ResultsViewController results)
        {
            _resultsViewController.continueButtonPressedEvent -= resultsViewController_continueButtonPressedEvent;
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);
        }

        private void resultsViewController_restartButtonPressedEvent(ResultsViewController results)
        {
            _resultsViewController.continueButtonPressedEvent -= resultsViewController_continueButtonPressedEvent;
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController, () => songDetail_didPressPlayButtonEvent(_lastPlayedBeatmapLevel, _lastPlayedCharacteristic, _lastPlayedDifficulty));
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = (standardLevelScenesTransitionSetupData.sceneSetupDataArray.First(x => x is GameplayCoreSceneSetupData) as GameplayCoreSceneSetupData).difficultyBeatmap;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Restart) SongUtils.PlaySong(map.level, map.parentDifficultyBeatmapSet.beatmapCharacteristic, map.difficulty, songFinishedCallback: SongFinished);
            else if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.None)
            {
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
                {
                    PlayerUtils.GetPlatformUserData((username, userId) => SubmitScoreWhenResolved(username, userId, results));

                    _menuLightsManager.SetColorPreset(_scoreLights, true);
                    _resultsViewController.Init(results, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += resultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += resultsViewController_restartButtonPressedEvent;
                }
                else if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                {
                    _menuLightsManager.SetColorPreset(_redLights, true);
                    _resultsViewController.Init(results, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += resultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += resultsViewController_restartButtonPressedEvent;
                }
                
                PresentViewController(_resultsViewController, null, true);
            }
        }

        private async void SubmitScoreWhenResolved(string username, ulong userId, LevelCompletionResults results)
        {
            var scores = ((await HostScraper.RequestResponse(EventHost, new Packet(new SubmitScore
            {
                Score = new Score
                {
                    EventId = Event.EventId,
                    Parameters = _currentParameters,
                    UserId = userId,
                    Username = username,
                    FullCombo = results.fullCombo,
                    _Score = results.modifiedScore,
                    Color = "#ffffff"
                }
            }), typeof(ScoreRequestResponse), username, userId)).SpecificPacket as ScoreRequestResponse).Scores;

            UnityMainThreadDispatcher.Instance().Enqueue(() => SetCustomLeaderboardScores(scores, userId));
        }

        private async void RequestLeaderboardWhenResolved(string username, ulong userId)
        {
            var scores = ((await HostScraper.RequestResponse(EventHost, new Packet(new ScoreRequest
            {
                EventId = Event.EventId,
                Parameters = _currentParameters
            }), typeof(ScoreRequestResponse), username, userId)).SpecificPacket as ScoreRequestResponse).Scores;

            UnityMainThreadDispatcher.Instance().Enqueue(() => SetCustomLeaderboardScores(scores, userId));
        }

        public void SetCustomLeaderboardScores(Score[] scores, ulong userId)
        {
            var place = 1;
            var indexOfme = -1;
            _customLeaderboard.SetScores(scores.Select(x =>
            {
                if (x.UserId == userId) indexOfme = place - 1;
                return new Views.CustomLeaderboardTable.CustomScoreData(x._Score, x.Username, place++, x.FullCombo, x.Color);
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
                SetLeftScreenViewController(null);
                SetRightScreenViewController(null);
                DismissViewController(_songDetail);
            }
            else DidFinishEvent?.Invoke();
        }
    }
}
