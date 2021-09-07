using IPA;
using IPA.Loader;
using SiraUtil;
using SiraUtil.Attributes;
using SiraUtil.Zenject;
using System;
using TournamentAssistant.Installers;
using TournamentAssistant.Managers;
using TournamentAssistantShared;
using Logger = IPA.Logging.Logger;

/**
 * Created by Moon on 8/5/2019
 * Base plugin class for the TournamentAssistant plugin
 * Intended to be the player-facing UI for tournaments, where
 * players' games can be handled by their match coordinators
 */

namespace TournamentAssistant
{
    [Plugin(RuntimeOptions.DynamicInit), Slog]
    public class Plugin
    {
        public string Name => SharedConstructs.Name;
        public string Version => SharedConstructs.Version;

        [Init]
        public Plugin(Logger logger, Zenjector zenjector, PluginMetadata metadata)
        {
            Config config = new();
            zenjector.On<PCAppInit>().Pseudo(Container =>
            {
                Container.Bind(typeof(IDisposable), typeof(PluginClient)).To<PluginClient>().AsSingle();
                Container.BindLoggerAsSiraLogger(logger);
                Container.BindInstance(config).AsCached();
                Container.BindInstance(new UBinder<Plugin, Random>(new Random())).AsCached();
                Container.BindInstance(new UBinder<Plugin, PluginMetadata>(metadata)).AsCached();
                Container.Bind<ILevelService>().To<TALevelService>().AsSingle();
            });
            zenjector.OnMenu<TAMenuInstaller>();
            zenjector.OnMenu<TAViewInstaller>();
            zenjector.OnGame<TAGameInstaller>().OnlyForStandard();

            //Enable legacy logger to use SiraLog
            GlobalConstants.IsPlugin = true;
        }

        [OnEnable]
        public void OnEnable()
        {

        }

        [OnDisable]
        public void OnDisable()
        {

        }
    }
}