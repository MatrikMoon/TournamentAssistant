using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Loader;
using SiraUtil.Tools;
using SiraUtil.Zenject;
using System;
using System.Threading.Tasks;
using TournamentAssistant.ViewControllers;
using TournamentAssistantShared;
using Zenject;

namespace TournamentAssistant.FlowCoordinators
{
    internal class ModeSelectionFlowCoordinator : FlowCoordinator
    {
        [Inject]
        private readonly SiraLog _siraLog = null!;

        [Inject]
        private readonly PatchNotesView _patchNotesView = null!;

        [Inject]
        private readonly SplashScreenView _splashScreenView = null!;

        [Inject]
        private readonly MainFlowCoordinator _mainFlowCoordinator = null!;

        [Inject]
        private readonly UBinder<Plugin, PluginMetadata> _pluginMetadata = null!;

        [Inject]
        private readonly ServerModeSelectionView _serverModeSelectionView = null!;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                showBackButton = true;
                SetTitle($"{nameof(TournamentAssistant)} v{_pluginMetadata.Value.HVersion}");
                ProvideInitialViewControllers(_serverModeSelectionView, rightScreenViewController: _patchNotesView);
            }
            CheckForUpdate().RunMainHeadless();
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _mainFlowCoordinator.DismissFlowCoordinator(this);
        }

        private async Task CheckForUpdate()
        {
            var newVersion = await Update.GetLatestRelease();
            await Task.Delay(500);
            if (Version.Parse(SharedConstructs.Version) < newVersion)
            {
                _siraLog.Info("TA is outdated. Showing version screen.");
                _splashScreenView.Status = $"Update required! You are on \'{_pluginMetadata.Value.HVersion}\', new version is \'{newVersion}\'\n" +
                        $"Visit https://github.com/MatrikMoon/TournamentAssistant/releases to download the new version";
                ReplaceTopViewController(_splashScreenView);
            }
        }
    }
}
