using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Loader;
using SiraUtil.Tools;
using SiraUtil.Zenject;
using System;
using System.Threading.Tasks;
using TournamentAssistant.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
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

        [Inject]
        private readonly TournamentRoomFlowCoodinator _tournamentRoomFlowCoordinator = null!;

        [Inject]
        private readonly ServerSelectionFlowCoordinator _serverSelectionFlowCoordinator = null!;

        private string? _status;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                showBackButton = true;
                SetTitle($"{nameof(TournamentAssistant)} v{_pluginMetadata.Value.HVersion}");
                ProvideInitialViewControllers(_serverModeSelectionView, rightScreenViewController: _patchNotesView);
#if !DEBUG
                CheckForUpdate().RunMainHeadless();
#endif
            }
            if (addedToHierarchy && _status != null)
            {
                _splashScreenView.Status = _status;
            }

            _serverModeSelectionView.TournamentClicked += ServerModeSelectionView_TournamentClicked;
            _serverModeSelectionView.BattleSaberClicked += ServerModeSelectionView_BattleSaberClicked;

            if (addedToHierarchy)
            {
                _tournamentRoomFlowCoordinator.DismissRequested += TournamentRoomFlowCoordinator_DismissRequested;
                _serverSelectionFlowCoordinator.DismissRequested += ServerSelectionFlowCoordinator_DismissRequested;
            }
        }

        private void TournamentRoomFlowCoordinator_DismissRequested()
        {
            DismissFlowCoordinator(_tournamentRoomFlowCoordinator);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                _serverSelectionFlowCoordinator.DismissRequested -= ServerSelectionFlowCoordinator_DismissRequested;
                _tournamentRoomFlowCoordinator.DismissRequested -= TournamentRoomFlowCoordinator_DismissRequested;
            }

            _serverModeSelectionView.BattleSaberClicked -= ServerModeSelectionView_BattleSaberClicked;
            _serverModeSelectionView.TournamentClicked -= ServerModeSelectionView_TournamentClicked;
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        private void ServerSelectionFlowCoordinator_DismissRequested()
        {
            _serverSelectionFlowCoordinator.HostSelected -= ServerSelectionFlowCoordinator_HostSelected_Tournament;
            _serverSelectionFlowCoordinator.HostSelected -= ServerSelectionFlowCoordinator_HostSelected_BattleSaber;
            DismissFlowCoordinator(_serverSelectionFlowCoordinator);
        }

        private void ServerModeSelectionView_TournamentClicked()
        {
            _serverSelectionFlowCoordinator.HostSelected += ServerSelectionFlowCoordinator_HostSelected_Tournament;
            PresentFlowCoordinator(_serverSelectionFlowCoordinator);
        }

        private void ServerModeSelectionView_BattleSaberClicked()
        {
            _serverSelectionFlowCoordinator.HostSelected += ServerSelectionFlowCoordinator_HostSelected_BattleSaber;
            PresentFlowCoordinator(_serverSelectionFlowCoordinator);
        }

        private void ServerSelectionFlowCoordinator_HostSelected_Tournament(CoreServer server)
        {
            _serverSelectionFlowCoordinator.HostSelected -= ServerSelectionFlowCoordinator_HostSelected_Tournament;
            DismissFlowCoordinator(_serverSelectionFlowCoordinator, immediately: true);
            _tournamentRoomFlowCoordinator.SetHost(server);
            PresentFlowCoordinator(_tournamentRoomFlowCoordinator, immediately: true);
        }

        private void ServerSelectionFlowCoordinator_HostSelected_BattleSaber(CoreServer server)
        {
            _serverSelectionFlowCoordinator.HostSelected -= ServerSelectionFlowCoordinator_HostSelected_BattleSaber;
            DismissFlowCoordinator(_serverSelectionFlowCoordinator, immediately: true);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _mainFlowCoordinator.DismissFlowCoordinator(this);
        }

        private async Task CheckForUpdate()
        {
            var newVersion = await Update.GetLatestRelease();
            if (Version.Parse(SharedConstructs.Version) < newVersion)
            {
                _siraLog.Info("TA is outdated. Showing version screen.");
                _status = $"Update required! You are on \'{_pluginMetadata.Value.HVersion}\', new version is \'{newVersion}\'\n" +
                        $"Visit https://github.com/MatrikMoon/TournamentAssistant/releases to download the new version";
                await Task.Delay(500); // We can't switch to a new view controller if one is in the middle of activating.
                ReplaceTopViewController(_splashScreenView);
                _splashScreenView.Status = _status;

                ProvideInitialViewControllers(_splashScreenView, rightScreenViewController: _patchNotesView);
            }
        }
    }
}