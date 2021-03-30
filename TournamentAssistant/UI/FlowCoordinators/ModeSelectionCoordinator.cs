using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Threading.Tasks;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ModeSelectionCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        private EventSelectionCoordinator _eventSelectionCoordinator;
        private ServerSelectionCoordinator _serverSelectionCoordinator;
        private ServerModeSelection _serverModeSelectionViewController;
        private PatchNotes _PatchNotesViewController;
        private SplashScreen _splashScreen;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                SetTitle($"TournamentAssistant v{SharedConstructs.Version}");
                showBackButton = true;

                _PatchNotesViewController = BeatSaberUI.CreateViewController<PatchNotes>();
                _serverModeSelectionViewController = BeatSaberUI.CreateViewController<ServerModeSelection>();
                _serverModeSelectionViewController.BattleSaberButtonPressed += ServerModeSelectionViewController_BattleSaberButtonPressed;
                _serverModeSelectionViewController.QualifierButtonPressed += ServerModeSelectionViewController_QualifierButtonPressed;
                _serverModeSelectionViewController.TournamentButtonPressed += ServerModeSelectionViewController_TournamentButtonPressed;

                ProvideInitialViewControllers(_serverModeSelectionViewController, null, _PatchNotesViewController);

                //Check for updates before contacting a server
                Task.Run(CheckForUpdate);
            }
        }

        private async void CheckForUpdate()
        {
            var newVersion = await Update.GetLatestRelease();
            if (Version.Parse(SharedConstructs.Version) < newVersion)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                    _splashScreen.StatusText = $"Update required! You are on \'{SharedConstructs.Version}\', new version is \'{newVersion}\'\n" +
                        $"Visit https://github.com/MatrikMoon/TournamentAssistant/releases to download the new version";
                    PresentViewController(_splashScreen);
                });
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is SplashScreen) DismissViewController(_splashScreen, immediately: true);

            DidFinishEvent?.Invoke();
        }

        private void ServerModeSelectionViewController_BattleSaberButtonPressed()
        {
            _serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ServerSelectionCoordinator>();
            _serverSelectionCoordinator.DestinationCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomSelectionCoordinator>();
            _serverSelectionCoordinator.DidFinishEvent += ServerSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_serverSelectionCoordinator);
        }

        private void ServerModeSelectionViewController_QualifierButtonPressed()
        {
            _eventSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<EventSelectionCoordinator>();
            _eventSelectionCoordinator.DidFinishEvent += EventSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_eventSelectionCoordinator);
        }

        private void ServerModeSelectionViewController_TournamentButtonPressed()
        {
            _serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ServerSelectionCoordinator>();
            _serverSelectionCoordinator.DestinationCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomCoordinator>();
            _serverSelectionCoordinator.DidFinishEvent += ServerSelectionCoordinator_DidFinishEvent;
            PresentFlowCoordinator(_serverSelectionCoordinator);
        }

        private void ServerSelectionCoordinator_DidFinishEvent()
        {
            _serverSelectionCoordinator.DidFinishEvent -= ServerSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_serverSelectionCoordinator);
        }

        private void EventSelectionCoordinator_DidFinishEvent()
        {
            _eventSelectionCoordinator.DidFinishEvent -= EventSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_eventSelectionCoordinator);
        }
    }
}
