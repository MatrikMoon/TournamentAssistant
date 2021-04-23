#pragma warning disable IDE0052
using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Collections.Generic;
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

        //Custom Leaderboard stuff
        public int _scoreboardPos;
        public int _maxScoreboardPos;
        private Score[] _Scores;
        private ulong _userId;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("Qualifier Room", ViewController.AnimationType.None);
                showBackButton = true;

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
                _songDetail.PlayPressed += SongDetail_didPressPlayButtonEvent;
                _songDetail.DisableCharacteristicControl = true;
                _songDetail.DisableDifficultyControl = true;
                _songDetail.DisablePlayButton = false;

                _customLeaderboard = BeatSaberUI.CreateViewController<CustomLeaderboard>();
                _customLeaderboard.ScoreboardPageUp += CustomLeaderboard_ScoreboardPageUp;
                _customLeaderboard.ScoreboardPageDown += CustomLeaderboard_ScoreboardPageDown;
                _customLeaderboard.ScoreboardReset += CustomLeaderboard_ScoreboardReset;
            }
            if (addedToHierarchy)
            {
                _songSelection.SetSongs(Event.QualifierMaps.ToList());
                ProvideInitialViewControllers(_songSelection);
            }
        }

        private void SongDetail_didPressPlayButtonEvent(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
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
                        _currentParameters.PlayerSettings.NoteJumpStartBeatOffset,
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.HideNoteSpawnEffect),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.AdaptiveSfx),
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects,
                        _currentParameters.PlayerSettings.Options.HasFlag(PlayerOptions.StaticLights) ? EnvironmentEffectsFilterPreset.NoEffects : EnvironmentEffectsFilterPreset.AllEffects
                    );
            }

            var songSpeed = GameplayModifiers.SongSpeed.Normal;
            if (_currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.SlowSong)) songSpeed = GameplayModifiers.SongSpeed.Slower;
            if (_currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastSong)) songSpeed = GameplayModifiers.SongSpeed.Faster;
            if (_currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.SuperFastSong)) songSpeed = GameplayModifiers.SongSpeed.SuperFast;

            var gameplayModifiers = new GameplayModifiers(
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.DemoNoFail),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.DemoNoObstacles),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.BatteryEnergy) ? GameplayModifiers.EnergyType.Battery : GameplayModifiers.EnergyType.Bar,
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoFail),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.InstaFail),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.FailOnClash),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoObstacles) ? GameplayModifiers.EnabledObstacleType.NoObstacles : GameplayModifiers.EnabledObstacleType.All,
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoBombs),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.FastNotes),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.StrictAngles),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.DisappearingArrows),
                songSpeed,
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.NoArrows),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.GhostNotes),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.ProMode),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.ZenMode),
                _currentParameters.GameplayModifiers.Options.HasFlag(GameOptions.SmallCubes)
            );

            var colorScheme = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetSelectedColorScheme() : null;

            //Disable scores if we need to
            if (((QualifierEvent.EventSettings)Event.Flags).HasFlag(QualifierEvent.EventSettings.DisableScoresaberSubmission)) BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(SharedConstructs.Name);

            SongUtils.PlaySong(level, characteristic, difficulty, playerData.overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSettings, SongFinished);
        }

        private void SongSelection_SongSelected(GameplayParameters parameters)
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
                    SetRightScreenViewController(_globalLeaderboard, ViewController.AnimationType.In);

                    //fill with placeholders before presenting
                    List<object> placeholder = new();
                    for (int i = 0; i < 10; i++)
                    {
                        LeaderboardText currentScore = new()
                        {
                            LeftText = string.Empty,
                            RightText = string.Empty,
                            TextColor = "white"
                        };
                        placeholder.Add(currentScore);
                    }
                    _customLeaderboard.FillWithEmpty(placeholder);

                    PlayerUtils.GetPlatformUserData(RequestLeaderboardWhenResolved);
                    SetLeftScreenViewController(_customLeaderboard, ViewController.AnimationType.In);
                });
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
            DismissViewController(_resultsViewController, finishedCallback: () => SongDetail_didPressPlayButtonEvent(_lastPlayedBeatmapLevel, _lastPlayedCharacteristic, _lastPlayedDifficulty));
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = (standardLevelScenesTransitionSetupData.sceneSetupDataArray.First(x => x is GameplayCoreSceneSetupData) as GameplayCoreSceneSetupData).difficultyBeatmap;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Restart) SongDetail_didPressPlayButtonEvent(_lastPlayedBeatmapLevel, _lastPlayedCharacteristic, _lastPlayedDifficulty);
            else if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.None)
            {
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
                {
                    PlayerUtils.GetPlatformUserData((username, userId) => SubmitScoreWhenResolved(username, userId, results));

                    _menuLightsManager.SetColorPreset(_scoreLights, true);
                    _resultsViewController.Init(results, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += ResultsViewController_restartButtonPressedEvent;
                }
                else if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                {
                    _menuLightsManager.SetColorPreset(_redLights, true);
                    _resultsViewController.Init(results, map, false, highScore);
                    _resultsViewController.continueButtonPressedEvent += ResultsViewController_continueButtonPressedEvent;
                    _resultsViewController.restartButtonPressedEvent += ResultsViewController_restartButtonPressedEvent;
                }

                PresentViewController(_resultsViewController, immediately: true);
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
            }), typeof(ScoreRequestResponse), username, userId)).SpecificPacket as ScoreRequestResponse).Scores.ToArray();

            LeaderboardSetVars(scores, userId);
            ScoreboardHandler();
        }

        private async void RequestLeaderboardWhenResolved(string username, ulong userId)
        {
            var scores = ((await HostScraper.RequestResponse(EventHost, new Packet(new ScoreRequest
            {
                EventId = Event.EventId,
                Parameters = _currentParameters
            }), typeof(ScoreRequestResponse), username, userId)).SpecificPacket as ScoreRequestResponse).Scores.ToArray();

            LeaderboardSetVars(scores, userId);
            ScoreboardHandler();
        }
        private void LeaderboardSetVars(Score[] Scores, ulong userId)
        {
            _Scores = Scores;
            _maxScoreboardPos = (int)Math.Ceiling(Decimal.Divide(Scores.Length, 10));
            _scoreboardPos = 0;
            _userId = userId;
        }
        private void CustomLeaderboard_ScoreboardReset()
        {
            _scoreboardPos = 0;
            ScoreboardHandler();
        }

        private void CustomLeaderboard_ScoreboardPageDown()
        {
            if (_scoreboardPos != _maxScoreboardPos - 1)
            {
                _scoreboardPos++;
                ScoreboardHandler();
            }
        }

        private void CustomLeaderboard_ScoreboardPageUp()
        {
            if (_scoreboardPos != 0)
            {
                _scoreboardPos--;
                ScoreboardHandler();
            }
        }
        private void ScoreboardHandler()
        {
            int playerPos = -1;
            List<Score> scores = new();
            Score playerScore = new()
            {
                UserId = _userId
            };

            int repeat = ((_Scores.Length - (_scoreboardPos * 10)) >= 10) ? 10 : 10 - (_Scores.Length - (_scoreboardPos * 10));
            for (int i = 0; i < repeat; i++)
            {
                try
                {
                    if (_Scores[i + (_scoreboardPos * 10)].UserId == _userId)
                    {
                        playerScore = _Scores[i];
                        playerPos = i + (_scoreboardPos * 10);
                    }
                    else
                    {
                        scores.Add(_Scores[i + (_scoreboardPos * 10)]);
                    }
                }
                catch (Exception e)
                {
                    TournamentAssistantShared.Logger.Error(e.ToString());
                }
            }
            UnityMainThreadDispatcher.Instance().Enqueue(() => SetLeaderboardScores(scores, playerPos, playerScore));
        }
        public void SetLeaderboardScores(List<Score> scores, int playerPos, Score playerScore)
        {
            _customLeaderboard.SetScores(scores, playerPos, playerScore, _scoreboardPos, _maxScoreboardPos);
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
                SetLeftScreenViewController(null, ViewController.AnimationType.Out);
                SetRightScreenViewController(null, ViewController.AnimationType.Out);
                DismissViewController(_songDetail);
            }
            else DidFinishEvent?.Invoke();
        }
    }
}
