using SiraUtil;
using TournamentAssistant.FlowCoordinators;
using TournamentAssistant.ViewControllers;
using Zenject;

namespace TournamentAssistant.Installers
{
    internal class TAViewInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind<PatchNotesView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<SplashScreenView>().FromNewComponentAsViewController().AsSingle();

            Container.Bind<ServerModeSelectionView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<ModeSelectionFlowCoordinator>().FromNewComponentOnNewGameObject(nameof(ModeSelectionFlowCoordinator)).AsSingle();

            Container.Bind<IPConnectionView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<ServerSelectionView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<ServerSelectionFlowCoordinator>().FromNewComponentOnNewGameObject(nameof(ServerSelectionFlowCoordinator)).AsSingle();

            Container.Bind<PlayerListView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<SongDetailView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<SongSelectionView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<TeamSelectionView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<OngoingGameListView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<TournamentRoomFlowCoodinator>().FromNewComponentOnNewGameObject(nameof(TournamentRoomFlowCoodinator)).AsSingle();
        }
    }
}