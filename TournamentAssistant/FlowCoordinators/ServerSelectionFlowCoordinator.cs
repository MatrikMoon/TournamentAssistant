using HMUI;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.ViewControllers;
using TournamentAssistantShared.Models;
using Zenject;

namespace TournamentAssistant.FlowCoordinators
{
    internal class ServerSelectionFlowCoordinator : FlowCoordinator
    {
        [Inject]
        protected readonly PluginClient _pluginClient = null!;

        [Inject]
        protected readonly PatchNotesView _patchNotesView = null!;

        [Inject]
        protected readonly IPConnectionView _ipConnectionView = null!;

        [Inject]
        protected readonly SplashScreenView _splashScreenView = null!;

        [Inject]
        protected readonly ServerSelectionView _serverSelectionView = null!;

        public event Action? DismissRequested;
        public event Action<CoreServer>? HostSelected;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                showBackButton = false;
                SetTitle("Server Selection");

                _splashScreenView.Status = "Gathering Server List...";
                ProvideInitialViewControllers(_splashScreenView, _ipConnectionView, _patchNotesView);
                PopulateList().RunMainHeadless();
            }

            _ipConnectionView.ServerSelected += ServerSelected;
            _serverSelectionView.ServerSelected += ServerSelected;
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            _ipConnectionView.ServerSelected -= ServerSelected;
            _serverSelectionView.ServerSelected -= ServerSelected;
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        private void ServerSelected(CoreServer server)
        {
            HostSelected?.Invoke(server);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DismissRequested?.Invoke();
        }

        private async Task PopulateList()
        {
            var servers = await _pluginClient.GetCoreServers();
            await Task.Delay(500);
            showBackButton = true;
            ReplaceTopViewController(_serverSelectionView);
            ProvideInitialViewControllers(_serverSelectionView, _ipConnectionView, _patchNotesView);
            _serverSelectionView.SetServers(servers.Keys.ToList());
        }
    }
}