using CustomUI.BeatSaber;
using System;
using System.Linq;
using TournamentAssistant.UI.ViewControllers;
using UnityEngine;
using VRUI;
using TournamentAssistant.Misc;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class MatchFlowCoordinator : FlowCoordinator
    {
        public event Action DidFinishEvent;

        private IntroFlowCoordinator _introFlowCoordinator;

        private PlayerDataModelSO _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private CampaignFlowCoordinator _campaignFlowCoordinator;

        private MatchViewController _matchViewController;
        private GeneralNavigationController _navigationController;
        private ResultsViewController _resultsViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Tournament Waiting Screen";

                _introFlowCoordinator = _introFlowCoordinator ?? Resources.FindObjectsOfTypeAll<IntroFlowCoordinator>().First();
                _resultsViewController = _resultsViewController ?? Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _playerDataModel = _playerDataModel ?? Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                _menuLightsManager = _menuLightsManager ?? Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = _soloFreePlayFlowCoordinator ?? Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _campaignFlowCoordinator = _campaignFlowCoordinator ?? Resources.FindObjectsOfTypeAll<CampaignFlowCoordinator>().First();

                _matchViewController = BeatSaberUI.CreateViewController<MatchViewController>();

                _navigationController = BeatSaberUI.CreateViewController<GeneralNavigationController>();
                _navigationController.didFinishEvent += (_) => DidFinishEvent?.Invoke();

                ProvideInitialViewControllers(_navigationController);

                SetViewControllersToNavigationConctroller(_navigationController, new VRUIViewController[] { _matchViewController });

                Plugin.client.LoadedSong += Client_LoadedSong;
            }
        }

        private void Client_LoadedSong(IBeatmapLevel level)
        {
            _matchViewController?.SetData(level);
        }

        private bool BSUtilsScoreDisabled()
        {
            return BS_Utils.Gameplay.ScoreSubmission.Disabled || BS_Utils.Gameplay.ScoreSubmission.ProlongedDisabled;
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = standardLevelScenesTransitionSetupData.difficultyBeatmap;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            var scoreLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsLightsPreset");
            var redLights = _campaignFlowCoordinator.GetField<MenuLightsPresetSO>("_newObjectiveLightsPreset");
            var defaultLights = _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

            //For the purpose of the tournament plugin, we'll do nothing if the end action is to restart
            if (results.levelEndAction != LevelCompletionResults.LevelEndAction.Restart)
            {
                if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) //Didn't quit and didn't die
                {
                    //If bs_utils disables score submission, we do too
                    if (IPA.Loader.PluginManager.AllPlugins.Any(x => x.Metadata.Name.ToLower() == "Beat Saber Utils".ToLower()))
                    {
                        if (BSUtilsScoreDisabled()) return;
                    }

                    //Scoresaber leaderboards
                    var platformLeaderboardsModel = Resources.FindObjectsOfTypeAll<PlatformLeaderboardsModel>().First();
                    var playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                    playerDataModel.playerData.playerAllOverallStatsData.soloFreePlayOverallStatsData.UpdateWithLevelCompletionResults(results);
                    playerDataModel.Save();

                    PlayerData currentLocalPlayer = playerDataModel.playerData;
                    IDifficultyBeatmap difficultyBeatmap = map;
                    GameplayModifiers gameplayModifiers = results.gameplayModifiers;
                    bool cleared = results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared;
                    string levelID = difficultyBeatmap.level.levelID;
                    BeatmapDifficulty difficulty = difficultyBeatmap.difficulty;
                    PlayerLevelStatsData playerLevelStatsData = currentLocalPlayer.GetPlayerLevelStatsData(levelID, difficulty, difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic);
                    bool result = playerLevelStatsData.highScore < results.modifiedScore;
                    playerLevelStatsData.IncreaseNumberOfGameplays();
                    if (cleared && result)
                    {
                        playerLevelStatsData.UpdateScoreData(results.modifiedScore, results.maxCombo, results.fullCombo, results.rank);
                        platformLeaderboardsModel.AddScoreFromComletionResults(difficultyBeatmap, results);
                    }
                }

                Action<ResultsViewController> resultsContinuePressed = null;
                resultsContinuePressed = (e) =>
                {
                    _resultsViewController.continueButtonPressedEvent -= resultsContinuePressed;
                    _menuLightsManager.SetColorPreset(defaultLights, true);
                    DismissViewController(_resultsViewController);
                };

                _menuLightsManager.SetColorPreset(scoreLights, true);
                _resultsViewController.Init(results, map, highScore);
                _resultsViewController.continueButtonPressedEvent += resultsContinuePressed;
                PresentViewController(_resultsViewController, null, true);
            }
        }
    }
}
