using BeatSaberMarkupLanguage;
using HMUI;
using System;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class TournamentSelectionCoordinator : FlowCoordinatorWithTournamentInfo, IFinishableFlowCoordinator
    {
        public FlowCoordinatorWithClient DestinationCoordinator { get; set; }

        private TournamentSelection _tournamentSelectionViewController;
        private IPConnection _ipConnectionViewController;
        private PatchNotes _patchNotesViewController;
        private SplashScreen _splashScreen;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                //Set up UI
                SetTitle(Plugin.GetLocalized("tournament_selection"), ViewController.AnimationType.None);

                _ipConnectionViewController = BeatSaberUI.CreateViewController<IPConnection>();
                _patchNotesViewController = BeatSaberUI.CreateViewController<PatchNotes>();
                _tournamentSelectionViewController = BeatSaberUI.CreateViewController<TournamentSelection>();

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.TitleText = Plugin.GetLocalized("tournament_list");
                _splashScreen.StatusText = Plugin.GetLocalized("gathering_tournament_list");

                ProvideInitialViewControllers(_splashScreen, _ipConnectionViewController, _patchNotesViewController);
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                _tournamentSelectionViewController.TournamentSelected -= JoinTournament;
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is IPConnection)
            {
                DismissViewController(topViewController, immediately: true);
                return;
            }

            if (topViewController is TournamentSelection)
            {
                DismissViewController(topViewController, immediately: true);
            }

            base.Dismiss();
        }

        private void JoinTournament(Tournament tournament)
        {
            DestinationCoordinator.DidFinishEvent += DestinationCoordinator_DidFinishEvent;
            DestinationCoordinator.TournamentId = tournament.Guid;
            DestinationCoordinator.Server = tournament.Server;
            PresentFlowCoordinator(DestinationCoordinator);
        }

        private void DestinationCoordinator_DidFinishEvent()
        {
            DestinationCoordinator.DidFinishEvent -= DestinationCoordinator_DidFinishEvent;
            DismissFlowCoordinator(DestinationCoordinator);
        }

        protected override void OnIndividualInfoScraped(Scraper.OnProgressData data) => UpdateScrapeCount(data.SucceededServers + data.FailedServers, data.TotalServers);

        protected override void OnInfoScraped(Scraper.OnProgressData data)
        {
            if (Tournaments != null && Tournaments.Count > 0)
            {
                _tournamentSelectionViewController.SetTournaments(Tournaments);
                _tournamentSelectionViewController.TournamentSelected += JoinTournament;

                PresentViewController(_tournamentSelectionViewController);
            }
            else
            {
                _splashScreen.StatusText = Plugin.GetLocalized("failed_to_get_tournaments");
            }
        }

        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"{Plugin.GetLocalized("gathering_data")} ({count} / {total})...";
        }
    }
}
