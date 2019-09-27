using CustomUI.BeatSaber;
using SongCore;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class TournamentFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator;

        private PlayerDataModelSO _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private CampaignFlowCoordinator _campaignFlowCoordinator;

        private SongViewController _songListViewController;
        private GeneralNavigationController _mainModNavigationController;
        private ResultsViewController _resultsViewController;

        private StandardLevelDetailViewController _levelDetailViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Tournament Waiting Screen";

                _mainFlowCoordinator = _mainFlowCoordinator ?? Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
                _resultsViewController = _resultsViewController ?? Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _playerDataModel = _playerDataModel ?? Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                _menuLightsManager = _menuLightsManager ?? Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = _soloFreePlayFlowCoordinator ?? Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _campaignFlowCoordinator = _campaignFlowCoordinator ?? Resources.FindObjectsOfTypeAll<CampaignFlowCoordinator>().First();

                _songListViewController = _songListViewController ?? BeatSaberUI.CreateViewController<SongViewController>();
                _songListViewController.CellSelected += mainViewController_CellSelected;

                _mainModNavigationController = BeatSaberUI.CreateViewController<GeneralNavigationController>();
                _mainModNavigationController.didFinishEvent += (_) => _mainFlowCoordinator.InvokeMethod("DismissFlowCoordinator", this, null, false);

                ProvideInitialViewControllers(_mainModNavigationController);

                SetViewControllersToNavigationConctroller(_mainModNavigationController, new VRUIViewController[] { _songListViewController });

                //Set up Client
                Config.LoadConfig();

                Action<string, ulong> onUsernameResolved = (username, _) =>
                {
                    if (Plugin.client?.Connected != true)
                    {
                        Plugin.client = new Client(Config.HostName, username);
                        Plugin.client.Start();
                    }
                    Plugin.client.LoadedSong += Client_LoadedSong;
                };

                PlayerUtils.GetPlatformUserData(onUsernameResolved);
            }
        }

        private void Client_LoadedSong(IBeatmapLevel level)
        {
            _songListViewController?.SetData(level);
            //_levelDetailViewController?.SetData(null, level, Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First().playerData, true);
        }

        private void mainViewController_CellSelected(IPreviewBeatmapLevel level)
        {
            /*var viewController = BeatSaberUI.CreateViewController<CustomDetailViewController>();
            viewController.rectTransform.sizeDelta = new Vector2(80f, 0f);
            PresentDetailViewController(viewController);*/
            if (_levelDetailViewController == null)
            {
                _levelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
                var _levelDetailView = _levelDetailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView");
                _levelDetailView.GetField<Button>("_playButton").gameObject.SetActive(false);
                _levelDetailView.GetField<Button>("_practiceButton").gameObject.SetActive(false);
            }
            var playerData = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First().playerData;
            _levelDetailViewController.SetData(null, level, playerData, true);
            PresentDetailViewController(_levelDetailViewController);
        }

        private void PresentDetailViewController(VRUIViewController viewController)
        {
            if (!viewController.isInViewControllerHierarchy)
            {
                PushViewControllerToNavigationController(_mainModNavigationController, viewController, null, false);
            }
        }

        public void PresentUI()
        {
            _mainFlowCoordinator = _mainFlowCoordinator ?? Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            _mainFlowCoordinator.InvokeMethod("PresentFlowCoordinatorOrAskForTutorial", this);
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
