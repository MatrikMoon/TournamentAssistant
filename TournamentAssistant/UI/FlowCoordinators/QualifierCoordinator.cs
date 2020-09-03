using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class QualifierCoordinator : FlowCoordinator
    {
        public event Action DidFinishEvent;

        //public Event Event { get; set; }

        private SongSelection _songSelection;
        private SplashScreen _splashScreen;
        private PlayerList _playerList;
        private SongDetail _songDetail;

        private PlayerDataModel _playerDataModel;
        private MenuLightsManager _menuLightsManager;
        private SoloFreePlayFlowCoordinator _soloFreePlayFlowCoordinator;
        private CampaignFlowCoordinator _campaignFlowCoordinator;

        private ResultsViewController _resultsViewController;
        private MenuLightsPresetSO _scoreLights;
        private MenuLightsPresetSO _redLights;
        private MenuLightsPresetSO _defaultLights;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                //Set up UI
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
                //_songSelection.SongSelected += songSelection_SongSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();

                _songDetail = BeatSaberUI.CreateViewController<SongDetail>();
                //_songDetail.PlayPressed += songDetail_didPressPlayButtonEvent;
                //_songDetail.DifficultyBeatmapChanged += songDetail_didChangeDifficultyBeatmapEvent;

                _playerList = BeatSaberUI.CreateViewController<PlayerList>();
            }
            if (activationType == ActivationType.AddedToHierarchy)
            {
                _splashScreen.StatusText = $"Downloading songs (\"{1} / {1}\")...";
                ProvideInitialViewControllers(_splashScreen);
            }
        }

        public void Dismiss()
        {
            ResetUI(); //Dismisses any presented view controllers
            DidFinishEvent?.Invoke();
        }

        private void ResetUI()
        {
            if (Plugin.IsInMenu())
            {
                //The results view and detail view aren't my own, they're the *real* views used in the
                //base game. As such, we should give them back them when we leave
                if (_resultsViewController.isInViewControllerHierarchy)
                {
                    _resultsViewController.GetField<Button>("_restartButton").gameObject.SetActive(true);
                    _menuLightsManager.SetColorPreset(_defaultLights, false);
                    DismissViewController(_resultsViewController, immediately: true);
                }

                if (_songDetail.isInViewControllerHierarchy) DismissViewController(_songDetail, immediately: true);

                //Re-enable back button if it's disabled
                var screenSystem = this.GetField<ScreenSystem>("_screenSystem", typeof(FlowCoordinator));
                if (screenSystem != null)
                {
                    var backButton = screenSystem.GetField<Button>("_backButton");
                    if (!backButton.interactable) backButton.interactable = true;
                }

                _splashScreen.StatusText = "Waiting for the coordinator to create your match...";
            }
        }
    }
}
