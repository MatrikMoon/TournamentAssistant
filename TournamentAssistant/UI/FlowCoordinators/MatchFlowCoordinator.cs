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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PlayerSaveData;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class MatchFlowCoordinator : FlowCoordinator
    {
        public Match Match { get; set; }
        public event Action DidFinishEvent;

        private IntroFlowCoordinator _introFlowCoordinator;

        private PlayerDataModel _playerDataModel;
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
                _playerDataModel = _playerDataModel ?? Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
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
                Plugin.client.ServerDisconnected += DismissMatchCoordinator;
            }
        }

        private void Client_PlaySong(IPreviewBeatmapLevel desiredLevel, BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme, bool useSync = false)
        {
            if (Plugin.IsInMenu())
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
            //TODO: If anything ever needs to see match updates, it's here
        }

        private void Client_MatchDeleted(Match match)
        {
            if (match == Match) DismissMatchCoordinator();
        }

        private void DismissMatchCoordinator()
        {
            if (Plugin.IsInMenu())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (_resultsViewController.isInViewControllerHierarchy)
                    {
                        _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                        _menuLightsManager.SetColorPreset(_defaultLights, false);
                        DismissViewController(_resultsViewController, immediately: true);
                    }
                if (_detailViewController != null && _detailViewController.isActivated) DismissViewController(_detailViewController, immediately: true);
                    DidFinishEvent?.Invoke();
                });
            }
        }

        private void Client_LoadedSong(IBeatmapLevel level)
        {
            if (Plugin.IsInMenu())
            {
                Action setData = () =>
                {
                    //If the player is still on the results screen, go ahead and boot them out
                    if (_resultsViewController.isInViewControllerHierarchy) resultsViewController_continueButtonPressedEvent(null);

                    _detailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
                    _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_playButton").gameObject.SetActive(false);
                    _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_practiceButton").gameObject.SetActive(false);
                    _detailViewController.SetData(level, true, true, true);
                    if (!_detailViewController.isActivated) PresentViewController(_detailViewController);
                };
                UnityMainThreadDispatcher.Instance().Enqueue(setData);
            }
        }

        private bool BSUtilsScoreDisabled()
        {
            return BS_Utils.Gameplay.ScoreSubmission.Disabled || BS_Utils.Gameplay.ScoreSubmission.ProlongedDisabled;
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData, LevelCompletionResults results)
        {
            standardLevelScenesTransitionSetupData.didFinishEvent -= SongFinished;

            var map = (standardLevelScenesTransitionSetupData.sceneSetupDataArray.First(x => x is GameplayCoreSceneSetupData) as GameplayCoreSceneSetupData).difficultyBeatmap;
            var localPlayer = _playerDataModel.playerData;
            var localResults = localPlayer.GetPlayerLevelStatsData(map.level.levelID, map.difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            var highScore = localResults.highScore < results.modifiedScore;

            //Send final score to Host
            if (Plugin.client.Connected)
            {
                Logger.Info($"SENDING FINAL SCORE: {results.modifiedScore}");
                Plugin.client.Self.CurrentScore = results.modifiedScore;
                var playerUpdate = new Event();
                playerUpdate.eventType = Event.EventType.PlayerFinishedSong;
                playerUpdate.changedObject = Plugin.client.Self;
                Plugin.client.Send(new Packet(playerUpdate));
            }

            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.None)
            {
                _menuLightsManager.SetColorPreset(_scoreLights, true);
                _resultsViewController.Init(results, map, false, highScore);
                _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(false);
                _resultsViewController.continueButtonPressedEvent += resultsViewController_continueButtonPressedEvent;
                PresentViewController(_resultsViewController, null, true);
            }
            else if (!Plugin.client.Connected || !Plugin.client.State.Matches.Contains(Match)) DismissMatchCoordinator();
        }

        private void resultsViewController_continueButtonPressedEvent(ResultsViewController _)
        {
            _resultsViewController.continueButtonPressedEvent -= resultsViewController_continueButtonPressedEvent;
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);

            if (!Plugin.client.Connected || !Plugin.client.State.Matches.Contains(Match)) DismissMatchCoordinator();
        }
    }
}
