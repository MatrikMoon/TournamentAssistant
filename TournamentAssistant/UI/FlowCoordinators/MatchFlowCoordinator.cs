using CustomUI.BeatSaber;
using System;
using System.Linq;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using UnityEngine;
using UnityEngine.UI;
using VRUI;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class MatchFlowCoordinator : FlowCoordinator
    {
        public Match Match { get; set; }
        public event Action DidFinishEvent;

        private IntroFlowCoordinator _introFlowCoordinator;

        private PlayerDataModelSO _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private CampaignFlowCoordinator _campaignFlowCoordinator;

        private MatchViewController _matchViewController;
        private ResultsViewController _resultsViewController;
        private StandardLevelDetailViewController _detailViewController;

        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _redLights;
        private MenuLightsPresetSO _defaultLights;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Match Room";

                _introFlowCoordinator = _introFlowCoordinator ?? Resources.FindObjectsOfTypeAll<IntroFlowCoordinator>().First();
                _resultsViewController = _resultsViewController ?? Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _matchViewController = _matchViewController ?? BeatSaberUI.CreateViewController<MatchViewController>();
                _playerDataModel = _playerDataModel ?? Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                _menuLightsManager = _menuLightsManager ?? Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = _soloFreePlayFlowCoordinator ?? Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _campaignFlowCoordinator = _campaignFlowCoordinator ?? Resources.FindObjectsOfTypeAll<CampaignFlowCoordinator>().First();

                _scoreLights = _scoreLights ?? _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsLightsPreset");
                _redLights = _redLights ?? _campaignFlowCoordinator.GetField<MenuLightsPresetSO>("_newObjectiveLightsPreset");
                _defaultLights = _defaultLights ?? _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

                ProvideInitialViewControllers(_matchViewController);

                Plugin.client.PlaySong += Client_PlaySong;
                Plugin.client.LoadedSong += Client_LoadedSong;
                Plugin.client.MatchDeleted += Client_MatchDeleted;
                Plugin.client.MatchInfoUpdated += Client_MatchInfoUpdated;
            }
        }

        private void Client_PlaySong(IPreviewBeatmapLevel desiredLevel, BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme, bool useSync = false)
        {
            //If we're using sync, set up for it
            Plugin.UseSyncController = useSync;

            //Reset score
            Logger.Info($"RESETTING SCORE: 0");
            Plugin.client.Self.CurrentScore = 0;
            var playerUpdate = new Event();
            playerUpdate.eventType = Event.EventType.PlayerUpdated;
            playerUpdate.changedObject = Plugin.client.Self;
            Plugin.client.Send(new Packet(playerUpdate));

            SongUtils.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSpecificSettings, SongFinished);
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                Plugin.client.LoadedSong -= Client_LoadedSong;
                Plugin.client.MatchDeleted -= Client_MatchDeleted;
                Plugin.client.MatchInfoUpdated -= Client_MatchInfoUpdated;
            }
        }

        private void Client_MatchInfoUpdated(Match match)
        {

        }

        private void Client_MatchDeleted(Match match)
        {
            if (match == Match) UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (_resultsViewController.isInViewControllerHierarchy)
                {
                    _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                    _menuLightsManager.SetColorPreset(_defaultLights, false);
                    DismissViewController(_resultsViewController, immediately: true);
                }
                if (_detailViewController.isInViewControllerHierarchy) DismissViewController(_detailViewController, immediately: true);
                DidFinishEvent?.Invoke();
            });
        }

        private void Client_LoadedSong(IBeatmapLevel level)
        {
            //If the player is still on the results screen, go ahead and boot them out
            if (_resultsViewController.isInViewControllerHierarchy) resultsViewController_continueButtonPressedEvent(null);

            Action setData = () =>
            {
                _detailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
                _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_playButton").gameObject.SetActive(false);
                _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_practiceButton").gameObject.SetActive(false);
                _detailViewController.SetData(null, level, _playerDataModel.playerData, true);
                if (!_detailViewController.isInViewControllerHierarchy) PresentViewController(_detailViewController);
            };
            UnityMainThreadDispatcher.Instance().Enqueue(setData);
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

                    //Send final score to Host
                    Logger.Info($"SENDING FINAL SCORE: {results.modifiedScore}");
                    Plugin.client.Self.CurrentScore = results.modifiedScore;
                    var playerUpdated = new Event();
                    playerUpdated.eventType = Event.EventType.PlayerFinishedSong;
                    playerUpdated.changedObject = Plugin.client.Self;
                    Plugin.client.Send(new Packet(playerUpdated));

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

                _menuLightsManager.SetColorPreset(_scoreLights, true);
                _resultsViewController.Init(results, map, highScore);
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(false);
                _resultsViewController.continueButtonPressedEvent += resultsViewController_continueButtonPressedEvent;
                PresentViewController(_resultsViewController, null, true);
            }
        }

        private void resultsViewController_continueButtonPressedEvent(ResultsViewController _)
        {
            _resultsViewController.continueButtonPressedEvent -= resultsViewController_continueButtonPressedEvent;
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);
        }
    }
}
