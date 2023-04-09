using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using System;
using System.Threading.Tasks;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;
using UnityEngine;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ModeSelectionCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        private EventSelectionCoordinator _eventSelectionCoordinator;
        private TournamentSelectionCoordinator _serverSelectionCoordinator;
        private ServerModeSelection _serverModeSelectionViewController;
        private PatchNotes _patchNotesViewController;
        private ServerMessage _serverMessage;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                SetTitle($"TournamentAssistant v{Constants.VERSION}");
                showBackButton = true;

                _patchNotesViewController = BeatSaberUI.CreateViewController<PatchNotes>();
                _serverModeSelectionViewController = BeatSaberUI.CreateViewController<ServerModeSelection>();
                _serverModeSelectionViewController.BattleSaberButtonPressed += ServerModeSelectionViewController_BattleSaberButtonPressed;
                _serverModeSelectionViewController.QualifierButtonPressed += ServerModeSelectionViewController_QualifierButtonPressed;
                _serverModeSelectionViewController.TournamentButtonPressed += ServerModeSelectionViewController_TournamentButtonPressed;

                ProvideInitialViewControllers(_serverModeSelectionViewController, null, _patchNotesViewController);

                //Check for updates before contacting a server
                Task.Run(CheckForUpdate);
            }
        }

        private async void CheckForUpdate()
        {
            var newVersion = await Update.GetLatestRelease();
            if (Version.Parse(Constants.VERSION) < newVersion)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    var message = new TournamentAssistantShared.Models.Packets.Command.ShowModal()
                    {
                        MessageTitle = Plugin.GetLocalized("update_required"),
                        MessageText = $"{Plugin.GetLocalized("update_required_new_version")} {newVersion}\n{Plugin.GetLocalized("visit_site_to_download_new_version")}"
                    };
                    _serverMessage = BeatSaberUI.CreateViewController<ServerMessage>();
                    _serverMessage.SetMessage(message);

                    FloatingScreen screen = FloatingScreen.CreateFloatingScreen(new Vector2(100, 50), false, new Vector3(0f, 0.9f, 2.4f), Quaternion.Euler(30f, 0f, 0f));
                    screen.SetRootViewController(_serverMessage, ViewController.AnimationType.None);
                });
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (_serverMessage?.screen) Destroy(_serverMessage.screen.gameObject);

            DidFinishEvent?.Invoke();
        }

        private void ServerModeSelectionViewController_BattleSaberButtonPressed()
        {
            _serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<TournamentSelectionCoordinator>();
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
            _serverSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<TournamentSelectionCoordinator>();
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
