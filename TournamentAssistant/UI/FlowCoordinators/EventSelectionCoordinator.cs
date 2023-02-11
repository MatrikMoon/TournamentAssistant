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
                SetTitle(Plugin.GetLocalized("event_selection"), ViewController.AnimationType.None);
                showBackButton = false;

                _qualifierSelection = BeatSaberUI.CreateViewController<ItemSelection>();
                _qualifierSelection.ItemSelected += itemSelection_ItemSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.StatusText = Plugin.GetLocalized("gathering_event_list");

                ProvideInitialViewControllers(_splashScreen);
            }
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
            showBackButton = true;
            _qualifierSelection.SetItems(
                ScrapedInfo
                .Where(x => x.Value.Events != null && x.Value.Events.Count > 0)
                .SelectMany(x => x.Value.Events)
                .Where(x => x.Guid == "7c49200c-0e73-4190-bdff-a12cd1e9845e")
                .Select(x => new ListItem { Text = x.Name, Details = x.Guild.Name, Identifier = $"{x.Guid}" }).ToList());
            PresentViewController(_qualifierSelection);
        }

        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"{Plugin.GetLocalized("gathering_data")} ({count} / {total})...";
        }

        private void itemSelection_ItemSelected(ListItem item)
        {
            var eventHostPair = ScrapedInfo.Where(x => x.Value.Events != null).First(x => x.Value.Events.Any(y => $"{y.Guid}" == item.Identifier));
            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
            _qualifierCoordinator.DidFinishEvent += qualifierCoordinator_DidFinishEvent;
            _qualifierCoordinator.Event = eventHostPair.Value.Events.First(x => $"{x.Guid}" == item.Identifier);
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
