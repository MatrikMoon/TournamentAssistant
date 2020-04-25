using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using TournamentAssistant.Misc;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class RoomSelectionCoordinator : FlowCoordinator
    {
        public event Action DidFinishEvent;

        private RoomSelection _roomSelection;
        private RoomCoordinator _roomCoordinator;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                //Set up UI
                title = "Room Selection Screen";
                showBackButton = true;

                _roomSelection = BeatSaberUI.CreateViewController<RoomSelection>();
                _roomSelection.MatchSelected += roomSelection_MatchSelected;
                _roomSelection.CreateMatchPressed += roomSelection_MatchCreated;
                _roomSelection.SetMatches(Plugin.client.State.Matches.ToList());

                Plugin.client.MatchCreated += Client_MatchCreated;
                Plugin.client.MatchInfoUpdated += Client_MatchInfoUpdated;
                Plugin.client.MatchDeleted += Client_MatchDeleted;

                ProvideInitialViewControllers(_roomSelection);
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.RemovedFromHierarchy)
            {
                _roomSelection.MatchSelected -= roomSelection_MatchSelected;
                _roomSelection.CreateMatchPressed -= roomSelection_MatchCreated;
                Plugin.client.MatchCreated -= Client_MatchCreated;
                Plugin.client.MatchDeleted -= Client_MatchDeleted;
            }
        }

        private void Client_MatchCreated(Match _)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _roomSelection.SetMatches(Plugin.client.State.Matches.ToList());
            });
        }

        //The match list item is used in click events, so it needs to be up to date as possible
        //(ie: have the most recent player lists) This shouln't get score updates as they're only directed to match players
        //We could potentially refresh the view here too if we ever include updatable data in the texts
        private void Client_MatchInfoUpdated(Match _)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _roomSelection.SetMatches(Plugin.client.State.Matches.ToList());
            });
        }

        private void Client_MatchDeleted(Match _)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _roomSelection.SetMatches(Plugin.client.State.Matches.ToList());
            });
        }

        private void roomSelection_MatchCreated()
        {
            var match = new Match()
            {
                Guid = Guid.NewGuid().ToString(),
                Leader = Plugin.client.Self,
                Players = new Player[] { Plugin.client.Self as Player }
            };

            Plugin.client.CreateMatch(match);

            if (_roomCoordinator == null)
            {
                _roomCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomCoordinator>();
                _roomCoordinator.DidFinishEvent += () => DismissFlowCoordinator(_roomCoordinator);
            }
            _roomCoordinator.Match = match;
            PresentFlowCoordinator(_roomCoordinator);
        }

        private void roomSelection_MatchSelected(Match match)
        {
            //Add ourself to the match and send the update
            match.Players = match.Players.ToList().Union(new Player[] { Plugin.client.Self as Player }).ToArray();

            Plugin.client.UpdateMatch(match);

            if (_roomCoordinator == null)
            {
                _roomCoordinator = BeatSaberUI.CreateFlowCoordinator<RoomCoordinator>();
                _roomCoordinator.DidFinishEvent += () => DismissFlowCoordinator(_roomCoordinator);
            }
            _roomCoordinator.Match = match;
            PresentFlowCoordinator(_roomCoordinator);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DidFinishEvent?.Invoke();
        }
    }
}
