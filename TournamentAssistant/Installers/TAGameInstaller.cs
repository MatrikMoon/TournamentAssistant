using SiraUtil;
using TournamentAssistant.Behaviours;
using TournamentAssistant.Models;
using Zenject;

namespace TournamentAssistant.Installers
{
    internal class TAGameInstaller : Installer
    {
        private readonly PluginClient _pluginClient;

        public TAGameInstaller(PluginClient pluginClient)
        {
            _pluginClient = pluginClient;
        }

        public override void InstallBindings()
        {
            var options = _pluginClient.ActiveMatchOptions;
            if (!_pluginClient.Connected || options == null)
                return;

            Container.Bind<ScreenOverlay>().FromNewComponentOnNewGameObject().AsSingle();
            if (_pluginClient.Connected)
            {
                Container.Bind<RoomData>().FromInstance(new RoomData(_pluginClient.ActiveMatch, options));
                _pluginClient.ActiveMatch = null;
                _pluginClient.ActiveMatchOptions = null;

                Container.BindInterfacesAndSelfTo<ScoreMonitor>().AsSingle();
                if (options.UseFloatingScoreboard)
                {
                    Container.BindInterfacesAndSelfTo<FloatingScoreScreen>().AsSingle();
                }
                if (options.DisableFailing)
                {
                    Container.BindInterfacesAndSelfTo<AntiFail>().AsSingle();
                }
                if (options.DisablePausing)
                {
                    Container.BindInterfacesAndSelfTo<AntiPause>().AsSingle();
                }
                else if (options.UseStreamSync)
                {
                    Container.BindInterfacesAndSelfTo<SyncHandler>().AsSingle();
                }
                Container.BindInterfacesAndSelfTo<LevelStateManager>().AsSingle();
            }
        }
    }
}
