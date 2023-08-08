using BeatSaberMarkupLanguage;
using HMUI;
using System.Threading.Tasks;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using TournamentAssistantShared.Models;
using Response = TournamentAssistantShared.Models.Packets.Response;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class TournamentSelectionCoordinator : FlowCoordinatorWithClient, IFinishableFlowCoordinator
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
            DestinationCoordinator.TournamentServer = tournament.Server;
            PresentFlowCoordinator(DestinationCoordinator);
        }

        private void DestinationCoordinator_DidFinishEvent()
        {
            DestinationCoordinator.DidFinishEvent -= DestinationCoordinator_DidFinishEvent;
            DismissFlowCoordinator(DestinationCoordinator);
        }

        protected override async Task ConnectedToServer(Response.Connect response)
        {
            await base.ConnectedToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _tournamentSelectionViewController.SetTournaments(Plugin.client.StateManager.GetTournaments());
                _tournamentSelectionViewController.TournamentSelected += JoinTournament;
                PresentViewController(_tournamentSelectionViewController);
            });
        }

        protected override async Task FailedToConnectToServer(Response.Connect response)
        {
            await base.FailedToConnectToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Message) ? response.Message : Plugin.GetLocalized("failed_initial_attempt");
            });
        }
    }
}
