using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using SiraUtil.Tools;
using System;
using TournamentAssistant.FlowCoordinators;
using Zenject;

namespace TournamentAssistant.Managers
{
    internal class MenuButtonManager : IInitializable, IDisposable
    {
        private readonly SiraLog _siraLog;
        private readonly MenuButton _menuButton;
        private readonly MainFlowCoordinator _mainFlowCoordinator;
        private readonly ModeSelectionFlowCoordinator _modeSelectionFlowCoordinator;

        public MenuButtonManager(SiraLog siraLog, MainFlowCoordinator mainFlowCoordinator, ModeSelectionFlowCoordinator modeSelectionFlowCoordinator)
        {
            _siraLog = siraLog;
            _mainFlowCoordinator = mainFlowCoordinator;
            _modeSelectionFlowCoordinator = modeSelectionFlowCoordinator;
            _menuButton = new MenuButton(nameof(TournamentAssistant), ButtonClicked);
        }

        public void Initialize()
        {
            _siraLog.Info("Adding button...");
            MenuButtons.instance.RegisterButton(_menuButton);
        }

        public void Dispose()
        {
            if (MenuButtons.IsSingletonAvailable && BSMLParser.IsSingletonAvailable)
                MenuButtons.instance.UnregisterButton(_menuButton);
        }

        private void ButtonClicked()
        {
            _siraLog.Debug($"Presenting {nameof(ModeSelectionFlowCoordinator)}");
            _mainFlowCoordinator.PresentFlowCoordinator(_modeSelectionFlowCoordinator);
        }
    }
}