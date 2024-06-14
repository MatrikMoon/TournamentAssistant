using BeatSaberMarkupLanguage;
using HMUI;
using System;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class ModeSelectionCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        public CoreServer Server { get; set; }
        public PluginClient Client { get; set; }

        private EventSelectionCoordinator _eventSelectionCoordinator;
        private QualifierCoordinator _qualifierCoordinator;
        private RoomCoordinator _roomCoordinator;
        private ModeSelection _modeSelectionViewController;
        private PatchNotes _patchNotesViewController;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                var tournament = Client.StateManager.GetTournament(Client.SelectedTournament);

                SetTitle($"TournamentAssistant v{Constants.PLUGIN_VERSION}");
                showBackButton = true;

                _patchNotesViewController = BeatSaberUI.CreateViewController<PatchNotes>();
                _modeSelectionViewController = BeatSaberUI.CreateViewController<ModeSelection>();
                _modeSelectionViewController.QualifierButtonPressed += ModeSelectionViewController_QualifierButtonPressed;
                _modeSelectionViewController.TournamentButtonPressed += ModeSelectionViewController_TournamentButtonPressed;

                ProvideInitialViewControllers(_modeSelectionViewController, null, _patchNotesViewController);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DismissChildren();
            DidFinishEvent?.Invoke();
        }

        private FlowCoordinator GetQualifierCoordinator()
        {
            // If there's only one qualifier, don't bother showing them the list
            var tournament = Client.StateManager.GetTournament(Client.SelectedTournament);
            if (tournament.Qualifiers.Count == 1)
            {
                var qualifier = tournament.Qualifiers[0];

                _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
                _qualifierCoordinator.Event = qualifier;
                _qualifierCoordinator.Server = tournament.Server;
                _qualifierCoordinator.Client = Client;
                _qualifierCoordinator.DidFinishEvent += QualifierCoordinator_DidFinishEvent;
                return _qualifierCoordinator;
            }
            else
            {
                _eventSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<EventSelectionCoordinator>();
                _eventSelectionCoordinator.Client = Client;
                _eventSelectionCoordinator.DidFinishEvent += EventSelectionCoordinator_DidFinishEvent;
                return _eventSelectionCoordinator;
            }
        }

        private FlowCoordinator GetTournamentCoordinator()
        {
            _roomCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomCoordinator>();
            _roomCoordinator.Server = Server;
            _roomCoordinator.Client = Client;
            _roomCoordinator.DidFinishEvent += RoomCoordinator_DidFinishEvent;
            return _roomCoordinator;
        }

        private void ModeSelectionViewController_QualifierButtonPressed()
        {
            PresentFlowCoordinator(GetQualifierCoordinator());
        }

        private void EventSelectionCoordinator_DidFinishEvent()
        {
            _eventSelectionCoordinator.DidFinishEvent -= EventSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_eventSelectionCoordinator);
        }

        private void QualifierCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= QualifierCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }

        private void ModeSelectionViewController_TournamentButtonPressed()
        {
            PresentFlowCoordinator(GetTournamentCoordinator());
        }

        private void RoomCoordinator_DidFinishEvent()
        {
            _roomCoordinator.DidFinishEvent -= RoomCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_roomCoordinator);
        }

        public void DismissChildren()
        {
            if (_roomCoordinator != null && IsFlowCoordinatorInHierarchy(_roomCoordinator))
            {
                _roomCoordinator.DismissChildren();
                DismissFlowCoordinator(_roomCoordinator, immediately: true);
            }

            if (_eventSelectionCoordinator != null && IsFlowCoordinatorInHierarchy(_eventSelectionCoordinator))
            {
                _eventSelectionCoordinator.DismissChildren();
                DismissFlowCoordinator(_eventSelectionCoordinator, immediately: true);
            }

            if (_qualifierCoordinator != null && IsFlowCoordinatorInHierarchy(_qualifierCoordinator))
            {
                _qualifierCoordinator.DismissChildren();
                DismissFlowCoordinator(_qualifierCoordinator, immediately: true);
            }

            while (topViewController is not ModeSelection)
            {
                DismissViewController(topViewController, immediately: true);
            }
        }
    }
}
