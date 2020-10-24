using BeatSaberMarkupLanguage;
using HMUI;
using System.Linq;
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

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                //Set up UI
                SetTitle("Event Selection", ViewController.AnimationType.None);
                showBackButton = true;

                _qualifierSelection = BeatSaberUI.CreateViewController<ItemSelection>();
                _qualifierSelection.ItemSelected += itemSelection_ItemSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.StatusText = "Gathering Event List...";

                ProvideInitialViewControllers(_splashScreen);
            }

            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        }

        public override void Dismiss()
        {
            //if (_qualifierCoordinator != null && IsFlowCoordinatorInHierarchy(_qualifierCoordinator)) _qualifierCoordinator.Dismiss();
            if (topViewController is ItemSelection) DismissViewController(topViewController, immediately: true);

            base.Dismiss();
        }

        protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

        protected override void OnIndividualInfoScraped(CoreServer host, State state, int count, int total) => UpdateScrapeCount(count, total);

        protected override void OnInfoScraped()
        {
            _qualifierSelection.SetItems(
                ScrapedInfo
                .Where(x => x.Value.Events != null && x.Value.Events.Length > 0)
                .SelectMany(x => x.Value.Events)
                .Select(x => new ListItem { Text = x.Name, Details = x.Guild.Name, Identifier = $"{x.EventId}" }).ToList());
            PresentViewController(_qualifierSelection);
        }

        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"Gathering Data ({count} / {total})...";
        }

        private void itemSelection_ItemSelected(ListItem item)
        {
            var eventHostPair = ScrapedInfo.First(x => x.Value.Events.Any(y => $"{y.EventId}" == item.Identifier));
            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
            _qualifierCoordinator.DidFinishEvent += qualifierCoordinator_DidFinishEvent;
            _qualifierCoordinator.Event = eventHostPair.Value.Events.First(x => $"{x.EventId}" == item.Identifier);
            _qualifierCoordinator.EventHost = eventHostPair.Key;
            PresentFlowCoordinator(_qualifierCoordinator);
        }

        private void qualifierCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= qualifierCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }
    }
}
