using SiraUtil;
using TournamentAssistant.Behaviors;
using Zenject;

namespace TournamentAssistant.Installers
{
    internal class TAGameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind<ScreenOverlay>().FromNewComponentOnNewGameObject().AsSingle();
            if (Plugin.client != null && Plugin.client.Connected)
            {
                Container.BindInterfacesAndSelfTo<ScoreMonitor>().AsSingle();
                if (Plugin.UseFloatingScoreboard)
                {
                    Container.BindInterfacesAndSelfTo<FloatingScoreScreen>().AsSingle();
                    Plugin.UseFloatingScoreboard = false;
                }
                if (Plugin.DisableFail)
                {
                    Container.BindInterfacesAndSelfTo<AntiFail>().AsSingle();
                    Plugin.DisableFail = false;
                }
                if (Plugin.DisablePause)
                {
                    Container.BindInterfacesAndSelfTo<AntiPause>().AsSingle();
                }
                else if (Plugin.UseSync)
                {
                    Container.BindInterfacesAndSelfTo<SyncHandler>().AsSingle();
                    Plugin.UseSync = false;
                }
                Container.BindInterfacesAndSelfTo<LevelStateManager>().AsSingle();
            }
        }
    }
}
