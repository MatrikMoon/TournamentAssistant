using BeatSaberMarkupLanguage;
using HMUI;
using System.Linq;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ServerSelectionCoordinator : FlowCoordinatorWithScrapedInfo, IFinishableFlowCoordinator
    {
        public FlowCoordinatorWithClient DestinationCoordinator { get; set; }

        private ServerSelection _serverSelectionViewController;
        private IPConnection _IPConnection;
        private SplashScreen _splashScreen;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                //Set up UI
                SetTitle("Server Selection", ViewController.AnimationType.None);
                showBackButton = false;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.StatusText = "Gathering Server List...";

                ProvideInitialViewControllers(_splashScreen);
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                _serverSelectionViewController.ServerSelected -= serverSelectionViewController_selectedServer;
                _serverSelectionViewController.ConnectViaIP -= _serverSelectionViewController_ConnectViaIP;
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

        private void serverSelectionViewController_selectedServer(CoreServer host)
        {
            DestinationCoordinator.DidFinishEvent += destinationCoordinator_DidFinishEvent;
            DestinationCoordinator.Host = host;
            PresentFlowCoordinator(DestinationCoordinator);
        }

        private void destinationCoordinator_DidFinishEvent()
        {
            DestinationCoordinator.DidFinishEvent -= destinationCoordinator_DidFinishEvent;
            DismissFlowCoordinator(DestinationCoordinator);
        }

        protected override void OnIndividualInfoScraped(CoreServer host, State state, int count, int total) => UpdateScrapeCount(count, total);

        protected override void OnInfoScraped()
        {
            showBackButton = true;
            _serverSelectionViewController = BeatSaberUI.CreateViewController<ServerSelection>();
            _serverSelectionViewController.ServerSelected += serverSelectionViewController_selectedServer;
            _serverSelectionViewController.ConnectViaIP += _serverSelectionViewController_ConnectViaIP;
            _serverSelectionViewController.SetServers(ScrapedInfo.Keys.Union(ScrapedInfo.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts)).ToList());
            PresentViewController(_serverSelectionViewController);
        }
        private void _serverSelectionViewController_ConnectViaIP()
        {
            _IPConnection = BeatSaberUI.CreateViewController<IPConnection>();
            _IPConnection.ServerSelected += _IPConnection_ServerSelected;
            PresentViewController(_IPConnection);
        }

        private void _IPConnection_ServerSelected(CoreServer host)
        {
            DestinationCoordinator.DidFinishEvent += destinationCoordinator_DidFinishEvent;
            DestinationCoordinator.Host = host;
            PresentFlowCoordinator(DestinationCoordinator);
        }


        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"Gathering Data ({count} / {total})...";
        }
    }
}
