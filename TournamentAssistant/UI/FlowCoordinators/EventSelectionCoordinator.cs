using BeatSaberMarkupLanguage;
using HMUI;
using System.Linq;
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistant.UI.ViewControllers;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class EventSelectionCoordinator : FlowCoordinator
    {
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
                        .Qualifiers.Select(x => new ListItem { Text = x.Name, Details = x.Guild.Name, Identifier = $"{x.Guid}" }).ToList());

                ProvideInitialViewControllers(_qualifierSelection);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            //if (_qualifierCoordinator != null && IsFlowCoordinatorInHierarchy(_qualifierCoordinator)) _qualifierCoordinator.Dismiss();
            if (topViewController is ItemSelection) DismissViewController(topViewController, immediately: true);
        }

        private void ItemSelection_ItemSelected(ListItem item)
        {
            var tournament = Client.StateManager.GetTournaments().Where(x => x.Qualifiers != null).First(x => x.Qualifiers.Any(y => $"{y.Guid}" == item.Identifier));
            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
            _qualifierCoordinator.DidFinishEvent += QualifierCoordinator_DidFinishEvent;
            _qualifierCoordinator.Event = tournament.Qualifiers.First(x => $"{x.Guid}" == item.Identifier);
            _qualifierCoordinator.Server = tournament.Server;
            PresentFlowCoordinator(_qualifierCoordinator);
        }

        private void QualifierCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= QualifierCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }
    }
}
