using BeatSaberMarkupLanguage;
using HMUI;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.UI.CustomListItems;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistant.Utilities;
using Response = TournamentAssistantShared.Models.Packets.Response;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class EventSelectionCoordinator : FlowCoordinatorWithClient
    {
        private SplashScreen _splashScreen;
        private ItemSelection _qualifierSelection;
        private QualifierCoordinator _qualifierCoordinator;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (addedToHierarchy)
            {
                //Set up UI
                SetTitle(Plugin.GetLocalized("event_selection"), ViewController.AnimationType.None);
                showBackButton = false;

                _qualifierSelection = BeatSaberUI.CreateViewController<ItemSelection>();
                _qualifierSelection.ItemSelected += ItemSelection_ItemSelected;

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.TitleText = Plugin.GetLocalized("event_list");
                _splashScreen.StatusText = Plugin.GetLocalized("gathering_event_list");

                ProvideInitialViewControllers(_splashScreen);
            }
        }

        public override void Dismiss()
        {
            //if (_qualifierCoordinator != null && IsFlowCoordinatorInHierarchy(_qualifierCoordinator)) _qualifierCoordinator.Dismiss();
            if (topViewController is ItemSelection) DismissViewController(topViewController, immediately: true);

            base.Dismiss();
        }

        protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

        protected override async Task ConnectedToServer(Response.Connect response)
        {
            await base.ConnectedToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                showBackButton = true;
                _qualifierSelection.SetItems(
                    Plugin.client.StateManager.GetTournaments()
                    .Where(x => x.Qualifiers != null && x.Qualifiers.Count > 0)
                    .SelectMany(x => x.Qualifiers)
                    .Select(x => new ListItem { Text = x.Name, Details = x.Guild.Name, Identifier = $"{x.Guid}" }).ToList());
                PresentViewController(_qualifierSelection);
            });
        }

        protected override async Task FailedToConnectToServer(Response.Connect response)
        {
            await base.FailedToConnectToServer(response);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                showBackButton = true;
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Message) ? response.Message : Plugin.GetLocalized("failed_initial_attempt");
            });
        }

        private void ItemSelection_ItemSelected(ListItem item)
        {
            var tournament = Plugin.client.StateManager.GetTournaments().Where(x => x.Qualifiers != null).First(x => x.Qualifiers.Any(y => $"{y.Guid}" == item.Identifier));
            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
            _qualifierCoordinator.DidFinishEvent += qualifierCoordinator_DidFinishEvent;
            _qualifierCoordinator.Event = tournament.Qualifiers.First(x => $"{x.Guid}" == item.Identifier);
            _qualifierCoordinator.EventServer = tournament.Server;
            PresentFlowCoordinator(_qualifierCoordinator);
        }

        private void qualifierCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= qualifierCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }
    }
}
