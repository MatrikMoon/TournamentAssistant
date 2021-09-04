using TournamentAssistant.Managers;
using Zenject;

namespace TournamentAssistant.Installers
{
    internal class TAMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<MenuButtonManager>().AsSingle();
        }
    }
}