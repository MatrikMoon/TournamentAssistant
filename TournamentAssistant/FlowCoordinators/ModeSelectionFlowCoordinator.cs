using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Loader;
using SiraUtil.Zenject;
using TournamentAssistant.ViewControllers;
using Zenject;

namespace TournamentAssistant.FlowCoordinators
{
    internal class ModeSelectionFlowCoordinator : FlowCoordinator
    {
        [Inject]
        private readonly PatchNotesView _patchNotesView = null!;

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

                ProvideInitialViewControllers(_serverModeSelectionView, _patchNotesView);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _mainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}
