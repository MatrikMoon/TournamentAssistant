using HMUI;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class RoomSelectionCoordinator : FlowCoordinator
    {
        public event Action DidFinishEvent;

        private RoomSelection _roomSelection;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Room Selection Screen";
                showBackButton = true;

                _roomSelection = _roomSelection ?? BeatSaberUI.CreateViewController<RoomSelection>();
                _roomSelection.MatchSelected += roomSelection_MatchSelected;
                _roomSelection.SetMatches(Plugin.client.State.Matches.ToList());

                Plugin.client.MatchCreated += Client_MatchCreated;
                Plugin.client.MatchDeleted += Client_MatchDeleted;

                ProvideInitialViewControllers(_roomSelection);
            }
        }

        private void Client_MatchCreated(TournamentAssistantShared.Models.Match _)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _roomSelection.SetMatches(Plugin.client.State.Matches.ToList());
            });
        }

        private void Client_MatchDeleted(TournamentAssistantShared.Models.Match _)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _roomSelection.SetMatches(Plugin.client.State.Matches.ToList());
            });
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                _roomSelection.MatchSelected -= roomSelection_MatchSelected;
                Plugin.client.MatchCreated -= Client_MatchCreated;
                Plugin.client.MatchDeleted -= Client_MatchDeleted;
            }
        }

        private void roomSelection_MatchSelected(TournamentAssistantShared.Models.Match obj)
        {
            
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DidFinishEvent?.Invoke();
        }
    }
}
