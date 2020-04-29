using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using BattleSaber.Misc;
using BattleSaber.UI.ViewControllers;
using BattleSaber.Utilities;
using BattleSaberShared;
using BattleSaberShared.Models;
using BattleSaberShared.Models.Packets;
using UnityEngine;
using UnityEngine.UI;
using Logger = BattleSaberShared.Logger;

namespace BattleSaber.UI.FlowCoordinators
{
    class TournamentMatchCoordinator : FlowCoordinator
    {
        public Match Match { get; set; }
        public event Action DidFinishEvent;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private CampaignFlowCoordinator _campaignFlowCoordinator;

        private SplashScreen _splashScreen;
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
                title = "Game Room";
                showBackButton = true;

                _resultsViewController = _resultsViewController ?? Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                _splashScreen = _splashScreen ?? BeatSaberUI.CreateViewController<SplashScreen>();
                _playerDataModel = _playerDataModel ?? Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
                _menuLightsManager = _menuLightsManager ?? Resources.FindObjectsOfTypeAll<MenuLightsManager>().First();
                _soloFreePlayFlowCoordinator = _soloFreePlayFlowCoordinator ?? Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                _campaignFlowCoordinator = _campaignFlowCoordinator ?? Resources.FindObjectsOfTypeAll<CampaignFlowCoordinator>().First();

                _scoreLights = _scoreLights ?? _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_resultsLightsPreset");
                _redLights = _redLights ?? _campaignFlowCoordinator.GetField<MenuLightsPresetSO>("_newObjectiveLightsPreset");
                _defaultLights = _defaultLights ?? _soloFreePlayFlowCoordinator.GetField<MenuLightsPresetSO>("_defaultLightsPreset");

                _splashScreen.StatusText = "Waiting for coordinator to create your match";
                ProvideInitialViewControllers(_splashScreen);

                Plugin.client.PlaySong += Client_PlaySong;
                Plugin.client.LoadedSong += Client_LoadedSong;
                Plugin.client.MatchCreated += Client_MatchCreated;
                Plugin.client.MatchDeleted += Client_MatchDeleted;
                Plugin.client.ServerDisconnected += DismissMatchCoordinator;
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                Plugin.client.PlaySong -= Client_PlaySong;
                Plugin.client.LoadedSong -= Client_LoadedSong;
                Plugin.client.MatchCreated -= Client_MatchCreated;
                Plugin.client.MatchDeleted -= Client_MatchDeleted;
                Plugin.client.ServerDisconnected -= DismissMatchCoordinator;
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DismissMatchCoordinator();
        }

        private void Client_MatchCreated(Match match)
        {
            if (match.Players.Contains(Plugin.client.Self))
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    Match = match;

                    //Player shouldn't be able to back out of a coordinated match
                    var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                    screenSystem.GetField<Button>("_backButton").interactable = false;

                    _splashScreen.StatusText = "Match has been created. Waiting for coordinator to select a song.";
                });
            }
        }

        private void Client_MatchDeleted(Match match)
        {
            if (match == Match) DismissMatchCoordinator();
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

        private void Client_PlaySong(IPreviewBeatmapLevel desiredLevel, BeatmapCharacteristicSO desiredCharacteristic, BeatmapDifficulty desiredDifficulty, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme colorScheme, bool useFloatingScoreboard = false, bool useSync = false)
        {
            if (Plugin.IsInMenu())
            {
                //Set up per-play settings
                Plugin.UseSyncController = useSync;
                Plugin.UseFloatingScoreboard = useFloatingScoreboard;

                //Reset score
                (Plugin.client.Self as Player).CurrentScore = 0;
                var playerUpdate = new Event();
                playerUpdate.eventType = Event.EventType.PlayerUpdated;
                playerUpdate.changedObject = Plugin.client.Self;
                Plugin.client.Send(new Packet(playerUpdate));

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //If the player is still on the results screen, go ahead and boot them out
                    if (_resultsViewController.isInViewControllerHierarchy) resultsViewController_continueButtonPressedEvent(null);

                    SongUtils.PlaySong(desiredLevel, desiredCharacteristic, desiredDifficulty, overrideEnvironmentSettings, colorScheme, gameplayModifiers, playerSpecificSettings, SongFinished);
                });
            }
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
                Logger.Debug($"SENDING FINAL SCORE: {results.modifiedScore}");
                (Plugin.client.Self as Player).CurrentScore = results.modifiedScore;
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

        private void DismissMatchCoordinator()
        {
            if (Plugin.IsInMenu())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    //The results view and detail view aren't my own, they're the *real* views used in the
                    //base game. As such, we should give them back them when we leave
                    if (_resultsViewController.isInViewControllerHierarchy)
                    {
                        _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                        _menuLightsManager.SetColorPreset(_defaultLights, false);
                        DismissViewController(_resultsViewController, immediately: true);
                    }
                    if (_detailViewController)
                    {
                        _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_playButton").gameObject.SetActive(true);
                        _detailViewController.GetField<StandardLevelDetailView>("_standardLevelDetailView").GetField<Button>("_practiceButton").gameObject.SetActive(true);

                        if (_detailViewController.isInViewControllerHierarchy) DismissViewController(_detailViewController, immediately: true);
                    }

                    //Re-enable back button if it's disabled
                    var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                    var backButton = screenSystem.GetField<Button>("_backButton");
                    if (!backButton.interactable) backButton.interactable = true;

                    DidFinishEvent?.Invoke();
                });
            }
        }

        private void resultsViewController_continueButtonPressedEvent(ResultsViewController _)
        {
            _resultsViewController.continueButtonPressedEvent -= resultsViewController_continueButtonPressedEvent;
            _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
            _menuLightsManager.SetColorPreset(_defaultLights, true);
            DismissViewController(_resultsViewController);

            if (!Plugin.client.Connected || !Plugin.client.State.Matches.Contains((Match)Match)) DismissMatchCoordinator();
        }
    }
}
