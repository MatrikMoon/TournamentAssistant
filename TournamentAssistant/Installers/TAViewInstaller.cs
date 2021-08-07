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
        }
    }
}