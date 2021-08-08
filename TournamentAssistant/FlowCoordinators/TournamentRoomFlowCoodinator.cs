using TournamentAssistant.ViewControllers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using Zenject;

namespace TournamentAssistant.FlowCoordinators
{
    internal class TournamentRoomFlowCoodinator : RoomFlowCoordinator
    {
        [Inject]
        private readonly PlayerListView _playerListView = null!;

        [Inject]
        private readonly SongDetailView _songDetailView = null!;

        [Inject]
        private readonly SplashScreenView _splashScreenView = null!;

        [Inject]
        private readonly SongSelectionView _songSelectionView = null!;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation)
            {
                SetTitle("Game Room");
                showBackButton = true;
            }
            if (addedToHierarchy)
            {
                _splashScreenView.Status = $"Connecting to \"{_host!.Name}\"...";
                ProvideInitialViewControllers(_splashScreenView);
            }
        }

        protected override void Connected(PluginClient sender, Player player, ConnectResponse response)
        {
            _splashScreenView.Status = $"Connected! Welcome, {player.Name}\n<size=70%>{_host!.Address}:{_host.Port}</size>";
        }

        protected override void FailedToConnect(PluginClient sender, ConnectResponse response)
        {

        }
    }
}