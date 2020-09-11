using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;

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
            SongUtils.PlaySong(level, characteristic, difficulty, songFinishedCallback: SongFinished);
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

                    if (_customLeaderboard == null)
                    {
                        _customLeaderboard = BeatSaberUI.CreateViewController<CustomLeaderboard>();
                    }
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
                PlayerUtils.GetPlatformUserData((username, userId) => OnUserDataResolved(username, userId, results));

                _menuLightsManager.SetColorPreset(_scoreLights, true);
                _resultsViewController.Init(results, map, false, highScore);
                _resultsViewController.continueButtonPressedEvent += resultsViewController_continueButtonPressedEvent;
                PresentViewController(_resultsViewController, null, true);
            }
        }

        private void OnUserDataResolved(string username, ulong userId, LevelCompletionResults results)
        {
            HostScraper.SendPacketToHost(EventHost, new Packet(new SubmitScore
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
            }), username, userId);
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
