using BeatSaberMarkupLanguage;
using HMUI;
using System.Linq;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ServerSelectionCoordinator : FlowCoordinatorWithScrapedInfo, IFinishableFlowCoordinator
    {
        public FlowCoordinatorWithClient DestinationCoordinator { get; set; }

        private ServerSelection _serverSelectionViewController;
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

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.StatusText = Plugin.GetLocalized("gathering_server_list");

                ProvideInitialViewControllers(_splashScreen, _ipConnectionViewController, _patchNotesViewController);
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                _serverSelectionViewController.ServerSelected -= ConnectToServer;
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is ServerSelection)
            {
                DismissViewController(topViewController, immediately: true);
                base.Dismiss();
            }
            if (topViewController is IPConnection) DismissViewController(topViewController, immediately: true);
        }

        private void ConnectToServer(CoreServer host)
        {
            DestinationCoordinator.DidFinishEvent += DestinationCoordinator_DidFinishEvent;
            DestinationCoordinator.Host = host;
            PresentFlowCoordinator(DestinationCoordinator);
        }

        private void DestinationCoordinator_DidFinishEvent()
        {
            DestinationCoordinator.DidFinishEvent -= DestinationCoordinator_DidFinishEvent;
            DismissFlowCoordinator(DestinationCoordinator);
        }

        protected override void OnIndividualInfoScraped(CoreServer host, State state, int count, int total) => UpdateScrapeCount(count, total);

        protected override void OnInfoScraped()
        {
            showBackButton = true;
            _serverSelectionViewController = BeatSaberUI.CreateViewController<ServerSelection>();
            _serverSelectionViewController.SetServers(ScrapedInfo.Keys.Union(ScrapedInfo.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts), new CoreServerEqualityComparer()).ToList());

            _serverSelectionViewController.ServerSelected += ConnectToServer;
            _ipConnectionViewController.ServerSelected += ConnectToServer;
            
            PresentViewController(_serverSelectionViewController);
        }

        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"{Plugin.GetLocalized("gathering_data")} ({count} / {total})...";
        }
    }
}
