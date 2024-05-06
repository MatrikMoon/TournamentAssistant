using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Linq;
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistant.UI.ViewControllers;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class EventSelectionCoordinator : FlowCoordinator, IFinishableFlowCoordinator
    {
        public event Action DidFinishEvent;

        public PluginClient Client { get; set; }

        private ItemSelection _qualifierSelection;
        private QualifierCoordinator _qualifierCoordinator;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                SetTitle(Plugin.GetLocalized("event_selection"), ViewController.AnimationType.None);
                showBackButton = true;

                _qualifierSelection = BeatSaberUI.CreateViewController<ItemSelection>();
                _qualifierSelection.ItemSelected += ItemSelection_ItemSelected;
                _qualifierSelection.SetItems(
                        Client.StateManager.GetTournament(Client.SelectedTournament)
                        .Qualifiers.Select(x => new ListItem { Text = x.Name, Identifier = $"{x.Guid}" }).ToList());

                ProvideInitialViewControllers(_qualifierSelection);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DismissChildren();
            DidFinishEvent?.Invoke();
        }

        public void DismissChildren()
        {
            if (_qualifierCoordinator != null && IsFlowCoordinatorInHierarchy(_qualifierCoordinator))
            {
                _qualifierCoordinator.DismissChildren();
                DismissFlowCoordinator(_qualifierCoordinator, immediately: true);
            }

            while (topViewController is not ItemSelection)
            {
                DismissViewController(topViewController, immediately: true);
            }
        }

        private void ItemSelection_ItemSelected(ListItem item)
        {
            var tournament = Client.StateManager.GetTournament(Client.SelectedTournament);
            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
            _qualifierCoordinator.DidFinishEvent += QualifierCoordinator_DidFinishEvent;
            _qualifierCoordinator.Event = tournament.Qualifiers.First(x => $"{x.Guid}" == item.Identifier);
            _qualifierCoordinator.Server = tournament.Server;
            _qualifierCoordinator.Client = Client;
            PresentFlowCoordinator(_qualifierCoordinator);
        }

        private void QualifierCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= QualifierCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }
    }
}
