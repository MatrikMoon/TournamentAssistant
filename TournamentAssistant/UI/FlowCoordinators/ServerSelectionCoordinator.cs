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
                _serverSelectionViewController.ServerSelected -= ServerSelectionViewController_selectedServer;
                _serverSelectionViewController.ConnectViaIP -= ServerSelectionViewController_ConnectViaIP;
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

        private void ServerSelectionViewController_selectedServer(CoreServer host)
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
            _serverSelectionViewController.ServerSelected += ServerSelectionViewController_selectedServer;
            _serverSelectionViewController.ConnectViaIP += ServerSelectionViewController_ConnectViaIP;
            _serverSelectionViewController.SetServers(ScrapedInfo.Keys.Union(ScrapedInfo.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts)).ToList());
            PresentViewController(_serverSelectionViewController);
        }
        private void ServerSelectionViewController_ConnectViaIP()
        {
            _IPConnection = BeatSaberUI.CreateViewController<IPConnection>();
            _IPConnection.ServerSelected += IPConnection_ServerSelected;
            PresentViewController(_IPConnection);
        }

        private void IPConnection_ServerSelected(CoreServer host)
        {
            DestinationCoordinator.DidFinishEvent += DestinationCoordinator_DidFinishEvent;
            DestinationCoordinator.Host = host;
            PresentFlowCoordinator(DestinationCoordinator);
        }


        private void UpdateScrapeCount(int count, int total)
        {
            _splashScreen.StatusText = $"Gathering Data ({count} / {total})...";
        }
    }
}
