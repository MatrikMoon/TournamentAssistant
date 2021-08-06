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
            Container.Bind<SplashScreenView>().FromNewComponentAsViewController().AsSingle();

            Container.Bind<PatchNotesView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<ServerModeSelectionView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<ModeSelectionFlowCoordinator>().FromNewComponentOnNewGameObject(nameof(ModeSelectionFlowCoordinator)).AsSingle();
        }
    }
}