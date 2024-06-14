using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities.Async;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistant.UI.ViewControllers;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Sockets;
using Response = TournamentAssistantShared.Models.Packets.Response;

namespace TournamentAssistant.UI.FlowCoordinators
{
    class TournamentSelectionCoordinator : FlowCoordinatorWithClient, IFinishableFlowCoordinator
    {
        private ModeSelectionCoordinator _modeSelectionCoordinator;
        private EventSelectionCoordinator _eventSelectionCoordinator;
        private QualifierCoordinator _qualifierCoordinator;
        private RoomCoordinator _roomCoordinator;

        private TournamentSelection _tournamentSelectionViewController;
        private IPConnection _ipConnectionViewController;
        private PatchNotes _patchNotesViewController;
        private SplashScreen _splashScreen;
        private UpdatePrompt _updatePrompt;

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

                _updatePrompt = BeatSaberUI.CreateViewController<UpdatePrompt>();

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

        private FlowCoordinator GetModeSelectionCoordinator(Tournament tournament, PluginClient client)
        {
            _modeSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<ModeSelectionCoordinator>();
            _modeSelectionCoordinator.DidFinishEvent += ModeSelectionCoordinator_DidFinishEvent;
            _modeSelectionCoordinator.Server = tournament.Server;
            _modeSelectionCoordinator.Client = client;
            return _modeSelectionCoordinator;
        }

        private FlowCoordinator GetTargetCoordinator(Tournament tournament, PluginClient client)
        {
            if (tournament.Settings.ShowTournamentButton && tournament.Settings.ShowQualifierButton)
            {
                return GetModeSelectionCoordinator(tournament, client);
            }
            else if (tournament.Settings.ShowTournamentButton)
            {
                return GetTournamentCoordinator();
            }
            else if (tournament.Settings.ShowQualifierButton)
            {
                return GetQualifierCoordinator();
            }
            else
            {
                return GetModeSelectionCoordinator(tournament, client);
            }
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

        private void RoomCoordinator_DidFinishEvent()
        {
            _roomCoordinator.DidFinishEvent -= RoomCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_roomCoordinator);
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
                        client.SelectedTournament = tournament.Guid;

                        await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                        {
                            if (joinResult.Type == Response.ResponseType.Success)
                            {
                                SetBackButtonInteractivity(true);
                                PresentFlowCoordinator(GetTargetCoordinator(client.StateManager.GetTournament(tournament.Guid), client), replaceTopViewController: true);
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
                    client.SelectedTournament = tournament.Guid;

                    await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    {
                        if (joinResult.Type == Response.ResponseType.Success)
                        {
                            SetBackButtonInteractivity(true);
                            PresentFlowCoordinator(GetTargetCoordinator(client.StateManager.GetTournament(tournament.Guid), client), replaceTopViewController: true);
                        }
                        else
                        {
                            SetBackButtonInteractivity(true);
                            _splashScreen.StatusText = $"Failed to join tournament: {joinResult.join.Message}";
                        }
                    });
                });
            }

            // If we're already in the tournament, just show the next screen
            else
            {
                PresentFlowCoordinator(GetTargetCoordinator(client.StateManager.GetTournament(tournament.Guid), client));
            }
        }

        private void ModeSelectionCoordinator_DidFinishEvent()
        {
            _modeSelectionCoordinator.DidFinishEvent -= ModeSelectionCoordinator_DidFinishEvent;
            DismissFlowCoordinator(_modeSelectionCoordinator);
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

                // If it's an incorrect version, attempt to update the plugin
                if (response?.Reason == Response.Connect.ConnectFailReason.IncorrectVersion)
                {
                    SetBackButtonInteractivity(false);
                    _updatePrompt.Cancel += UpdatePrompt_Cancel;
                    PresentViewController(_updatePrompt);
                }
            });

            // Retry
            // _ = Client.Connect();
        }

        private void UpdatePrompt_Cancel()
        {
            SetBackButtonInteractivity(true);
            DismissViewController(_updatePrompt);
        }

        public override void Dismiss()
        {
            if (_modeSelectionCoordinator != null && IsFlowCoordinatorInHierarchy(_modeSelectionCoordinator))
            {
                _modeSelectionCoordinator.DismissChildren();
                DismissFlowCoordinator(_modeSelectionCoordinator, immediately: true);
            }

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

            if (topViewController is UpdatePrompt)
            {
                DismissViewController(_updatePrompt, immediately: true);
            }

            if (topViewController is TournamentSelection)
            {
                DismissViewController(_tournamentSelectionViewController, immediately: true);
            }

            if (topViewController is SplashScreen)
            {
                DismissViewController(_splashScreen, immediately: true);
            }

            SetBackButtonInteractivity(true);

            base.Dismiss();
        }
    }
}
