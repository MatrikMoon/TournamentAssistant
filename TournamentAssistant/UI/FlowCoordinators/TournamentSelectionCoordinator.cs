using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities.Async;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using Response = TournamentAssistantShared.Models.Packets.Response;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class TournamentSelectionCoordinator : FlowCoordinatorWithClient, IFinishableFlowCoordinator
    {
        private QualifierCoordinator _qualifierCoordinator;

        private TournamentSelection _tournamentSelectionViewController;
        private IPConnection _ipConnectionViewController;
        private PatchNotes _patchNotesViewController;
        private SplashScreen _splashScreen;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (addedToHierarchy)
            {
                Server = new CoreServer() { Address = Constants.MASTER_SERVER, Port = Constants.MASTER_PORT };

                SetTitle(Plugin.GetLocalized("tournament_selection"), ViewController.AnimationType.None);

                _ipConnectionViewController = BeatSaberUI.CreateViewController<IPConnection>();
                _patchNotesViewController = BeatSaberUI.CreateViewController<PatchNotes>();
                _tournamentSelectionViewController = BeatSaberUI.CreateViewController<TournamentSelection>();

                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.TitleText = Plugin.GetLocalized("tournament_list");
                _splashScreen.StatusText = Plugin.GetLocalized("gathering_tournament_list");

                ProvideInitialViewControllers(_splashScreen, _ipConnectionViewController, _patchNotesViewController);
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

            if (removedFromHierarchy)
            {
                _tournamentSelectionViewController.TournamentSelected -= JoinTournament;
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController is IPConnection)
            {
                DismissViewController(topViewController, immediately: true);
                return;
            }

            if (topViewController is TournamentSelection)
            {
                DismissViewController(topViewController, immediately: true);
            }

            base.Dismiss();
        }

        private void JoinTournament(Tournament tournament)
        {
            var client = Client;

            // If the target tournament is in a different server, we'll need to connect a new client
            if (tournament.Server.Address != Client.Endpoint || tournament.Server.Port != Client.Port)
            {
                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>();
                _splashScreen.TitleText = Plugin.GetLocalized("tournament_list");
                _splashScreen.StatusText = "Joining tournament...";
                SetBackButtonInteractivity(false);
                PresentViewController(_splashScreen, immediately: true);

                client = new PluginClient(tournament.Server.Address, tournament.Server.Port);

                Task.Run(async () =>
                {
                    var connectResult = await client.Connect();

                    if (connectResult.Type == Response.ResponseType.Success)
                    {
                        var joinResult = await client.JoinTournament(tournament.Guid);

                        await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                        {
                            if (joinResult.Type == Response.ResponseType.Success)
                            {
                                _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
                                _qualifierCoordinator.DidFinishEvent += ModeSelectionCoordinator_DidFinishEvent;
                                _qualifierCoordinator.Server = tournament.Server;
                                _qualifierCoordinator.Client = client;
                                SetBackButtonInteractivity(true);

                                // TODO: proper event picking
                                Logger.Success(tournament.Qualifiers.Count);
                                _qualifierCoordinator.Event = client.StateManager.GetTournament(tournament.Guid).Qualifiers[0];

                                PresentFlowCoordinator(_qualifierCoordinator, replaceTopViewController: true);
                            }
                            else
                            {
                                SetBackButtonInteractivity(true);
                                _splashScreen.StatusText = $"Failed to join tournament: {joinResult.join.Message}";
                            }
                        });
                    }
                    else
                    {
                        await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                        {
                            SetBackButtonInteractivity(true);
                            _splashScreen.StatusText = "Failed to connect to server";
                        });
                    }
                });
            }

            // If the user is not already in the tournament, join it
            else if (!client.StateManager.GetTournament(tournament.Guid).Users.Any(x => x.Guid == client.StateManager.GetSelfGuid()))
            {
                _splashScreen = BeatSaberUI.CreateViewController<SplashScreen>(); 
                _splashScreen.TitleText = Plugin.GetLocalized("tournament_list");
                _splashScreen.StatusText = "Joining tournament...";
                SetBackButtonInteractivity(false);
                PresentViewController(_splashScreen, immediately: true);

                Task.Run(async () =>
                {
                    var joinResult = await client.JoinTournament(tournament.Guid);

                    await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    {
                        if (joinResult.Type == Response.ResponseType.Success)
                        {
                            _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
                            _qualifierCoordinator.DidFinishEvent += ModeSelectionCoordinator_DidFinishEvent;
                            _qualifierCoordinator.Server = tournament.Server;
                            _qualifierCoordinator.Client = client;
                            SetBackButtonInteractivity(true);

                            // TODO: proper event picking
                            _qualifierCoordinator.Event = client.StateManager.GetTournament(tournament.Guid).Qualifiers[0];

                            PresentFlowCoordinator(_qualifierCoordinator, replaceTopViewController: true);
                        }
                        else
                        {
                            SetBackButtonInteractivity(true);
                            _splashScreen.StatusText = $"Failed to join tournament: {joinResult.join.Message}";
                        }
                    });
                });
            }

            // If we're already in the tournament, just show the qualifiers
            else
            {
                _qualifierCoordinator = BeatSaberUI.CreateFlowCoordinator<QualifierCoordinator>();
                _qualifierCoordinator.DidFinishEvent += ModeSelectionCoordinator_DidFinishEvent;
                _qualifierCoordinator.Server = tournament.Server;
                _qualifierCoordinator.Client = client;

                // TODO: proper event picking
                _qualifierCoordinator.Event = client.StateManager.GetTournament(tournament.Guid).Qualifiers[0];

                PresentFlowCoordinator(_qualifierCoordinator);
            }
        }

        // TODO: makes more sense when qualifierCoordinator is replaced with modeCoordinator
        private void ModeSelectionCoordinator_DidFinishEvent()
        {
            _qualifierCoordinator.DidFinishEvent -= ModeSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_qualifierCoordinator);
        }

        protected override async Task ConnectedToServer(Response.Connect response)
        {
            await base.ConnectedToServer(response);

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _tournamentSelectionViewController.SetTournaments(Client.StateManager.GetTournaments());
                _tournamentSelectionViewController.TournamentSelected += JoinTournament;
                PresentViewController(_tournamentSelectionViewController);
            });
        }

        protected override async Task FailedToConnectToServer(Response.Connect response)
        {
            await base.FailedToConnectToServer(response);

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                _splashScreen.StatusText = !string.IsNullOrEmpty(response?.Message) ? response.Message : Plugin.GetLocalized("failed_initial_attempt");
            });


            // Retry
            // _ = Client.Connect();
        }

        public override void Dismiss()
        {
            if (_qualifierCoordinator != null && IsFlowCoordinatorInHierarchy(_qualifierCoordinator))
            {
                _qualifierCoordinator.DismissChildren();

                DismissFlowCoordinator(_qualifierCoordinator, immediately: true);
            }

            if (_tournamentSelectionViewController.isInViewControllerHierarchy)
            {
                DismissViewController(_tournamentSelectionViewController, immediately: true);
            }

            base.Dismiss();
        }
    }
}
