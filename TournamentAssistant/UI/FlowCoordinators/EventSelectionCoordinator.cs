using BeatSaberMarkupLanguage;
using HMUI;
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class EventSelectionCoordinator : FlowCoordinatorWithScrapedInfo
    {
        private SplashScreen _splashScreen;
        private ItemSelection _qualifierSelection;
        private QualifierCoordinator _qualifierCoordinator;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Event Selection";
                showBackButton = true;

                _qualifierSelection = BeatSaberUI.CreateViewController<ItemSelection>();
                _qualifierSelection.ItemSelected += itemSelection_ItemSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                UpdateScrapeCount(0, 0);

                ProvideInitialViewControllers(_splashScreen);
            }

            base.DidActivate(firstActivation, activationType);
        }

        public override void Dismiss()
        {
            if (_qualifierCoordinator != null && IsFlowCoordinatorInHierarchy(_qualifierCoordinator)) _qualifierCoordinator.Dismiss();
            if (topViewController is ItemSelection) DismissViewController(topViewController, immediately: true);

            base.Dismiss();
        }

        protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

        protected override void OnIndividualInfoScraped(CoreServer host, State state, int count, int total)
        {
            UpdateScrapeCount(count, total);
        }

        protected override void OnInfoScraped()
        {
            PresentViewController(_qualifierSelection);
        }

        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"Gathering Data ({count} / {total})...";
        }

        private void itemSelection_ItemSelected(ListItem item)
        {
            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
            _qualifierCoordinator.DidFinishEvent += qualifierCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_qualifierCoordinator);
        }

        private void qualifierCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= qualifierCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }
    }
}
