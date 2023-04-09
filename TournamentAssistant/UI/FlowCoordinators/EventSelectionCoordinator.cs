using BeatSaberMarkupLanguage;
using HMUI;
using System.Linq;
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class EventSelectionCoordinator : FlowCoordinatorWithTournamentInfo
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

        protected override void OnIndividualInfoScraped(Scraper.OnProgressData data) => UpdateScrapeCount(data.SucceededServers + data.FailedServers, data.TotalServers);

        protected override void OnInfoScraped(Scraper.OnProgressData data)
        {
            showBackButton = true;
            _qualifierSelection.SetItems(
                Tournaments
                .Where(x => x.Tournament.Qualifiers != null && x.Tournament.Qualifiers.Count > 0)
                .SelectMany(x => x.Tournament.Qualifiers)
                .Select(x => new ListItem { Text = x.Name, Details = x.Guild.Name, Identifier = $"{x.Guid}" }).ToList());
            PresentViewController(_qualifierSelection);
        }

        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"{Plugin.GetLocalized("gathering_data")} ({count} / {total})...";
        }

        private void itemSelection_ItemSelected(ListItem item)
        {
            var server = Tournaments.Where(x => x.Tournament.Qualifiers != null).First(x => x.Tournament.Qualifiers.Any(y => $"{y.Guid}" == item.Identifier));
            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
            _qualifierCoordinator.DidFinishEvent += qualifierCoordinator_DidFinishEvent;
            _qualifierCoordinator.Event = server.Tournament.Qualifiers.First(x => $"{x.Guid}" == item.Identifier);
            _qualifierCoordinator.EventServer = new CoreServer
            {
                Address = server.Address,
                Port = int.Parse(server.Port),
                Name = $"{server.Address}:{server.Port}", //Probably don't need this either, yet at least
                WebsocketPort = -1 //Probably don't need it?
            };
            PresentFlowCoordinator(_qualifierCoordinator);
        }

        private void qualifierCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= qualifierCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }
    }
}
