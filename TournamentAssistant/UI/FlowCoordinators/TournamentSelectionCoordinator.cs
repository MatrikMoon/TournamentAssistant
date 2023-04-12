using BeatSaberMarkupLanguage;
using HMUI;
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
                SetTitle(Plugin.GetLocalized("server_selection"), ViewController.AnimationType.None);

                showBackButton = false;

                _ipConnectionViewController = BeatSaberUI.CreateViewController<IPConnection>();
                _patchNotesViewController = BeatSaberUI.CreateViewController<PatchNotes>();
                _tournamentSelectionViewController = BeatSaberUI.CreateViewController<TournamentSelection>();

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.StatusText = Plugin.GetLocalized("gathering_server_list");

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
            if (topViewController is TournamentSelection)
            {
                DismissViewController(topViewController, immediately: true);
                base.Dismiss();
            }
            if (topViewController is IPConnection) DismissViewController(topViewController, immediately: true);
        }

        private void JoinTournament(Scraper.TournamentWithServerInfo tournament)
        {
            DestinationCoordinator.DidFinishEvent += DestinationCoordinator_DidFinishEvent;
            DestinationCoordinator.TournamentId = tournament.Tournament.Guid;
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
            showBackButton = true;
            _tournamentSelectionViewController.SetTournaments(Tournaments);
            _tournamentSelectionViewController.TournamentSelected += JoinTournament;

            PresentViewController(_tournamentSelectionViewController);
        }

        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"{Plugin.GetLocalized("gathering_data")} ({count} / {total})...";
        }
    }
}
