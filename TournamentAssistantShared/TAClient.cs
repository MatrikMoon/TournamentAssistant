using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantShared
{
    public class TAClient
    {
        public event Func<Response.Connect, Task> ConnectedToServer;
        public event Func<Response.Connect, Task> FailedToConnectToServer;
        public event Func<Task> ServerDisconnected;

        public event Func<Response.Join, Task> JoinedTournament;
        public event Func<Response.Join, Task> FailedToJoinTournament;

        public event Func<Response.ImagePreloaded, Guid, Task> ImagePreloaded;
        public event Func<Command.ShowModal, Task> ShowModal;
        public event Func<Push.SongFinished, Task> PlayerFinishedSong;
        public event Func<RealtimeScore, Task> RealtimeScoreReceived;

        public StateManager StateManager { get; set; }

        public bool Connected => client?.Connected ?? false;

        protected Client client;
        private string _authToken;

        private Timer _heartbeatTimer = new();
        private bool _shouldHeartbeat;
        private string _endpoint;
        private int _port;

        public TAClient(string endpoint, int port)
        {
            _endpoint = endpoint;
            _port = port;
        }

        public void setAuthToken(string authToken)
        {
            _authToken = authToken;
        }

        //Blocks until connected (or failed), then returns
        public async Task Start()
        {
            _shouldHeartbeat = true;
            _heartbeatTimer.Interval = 10000;
            _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;

            await ConnectToServer();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            //Send needs to be awaited so it will properly catch exceptions, but we can't make this timer callback async. So, we do this.
            async Task timerAction()
            {
                try
                {
                    await SendToServer(new Packet
                    {
                        Command = new Command
                        {
                            Heartbeat = true
                        }
                    });
                }
                catch (Exception e)
                {
                    Logger.Debug("HEARTBEAT FAILED");
                    Logger.Debug(e.ToString());

                    await ConnectToServer();
                }
            }
            Task.Run(timerAction);
        }

        private async Task ConnectToServer()
        {
            //Don't heartbeat while connecting
            _heartbeatTimer.Stop();

            try
            {
                client = new Client(_endpoint, _port);
                client.PacketReceived += Client_PacketWrapperReceived;
                client.ServerConnected += Client_ServerConnected;
                client.ServerFailedToConnect += Client_ServerFailedToConnect;
                client.ServerDisconnected += Client_ServerDisconnected;

                await client.Start();
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to connect to server. Retrying...");
                Logger.Debug(e.ToString());
            }
        }

        private async Task Client_ServerConnected()
        {
            //Resume heartbeat when connected
            if (_shouldHeartbeat) _heartbeatTimer.Start();

            /*Self = new User
            {
                Name = username,
                ClientType = clientType,
                UserId = userId
            };
            Self.ModLists.AddRange(modList);*/

            await SendToServer(new Packet
            {
                Request = new Request
                {
                    connect = new Request.Connect
                    {
                        ClientVersion = Constants.VERSION_CODE
                    }
                }
            });
        }

        private async Task Client_ServerFailedToConnect()
        {
            //Resume heartbeat if we fail to connect
            //Basically the same as just doing another connect here...
            //But with some extra delay. I don't really know why
            //I'm doing it this way
            if (_shouldHeartbeat) _heartbeatTimer.Start();

            if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(null);
        }

        private async Task Client_ServerDisconnected()
        {
            Logger.Debug("SystemClient: Server disconnected!");
            if (ServerDisconnected != null) await ServerDisconnected.Invoke();
        }

        public void Shutdown()
        {
            client?.Shutdown();
            _heartbeatTimer.Stop();

            //If the client was connecting when we shut it down, the FailedToConnect event might resurrect the heartbeat without this
            _shouldHeartbeat = false;
        }

        public Task SendAndGetResponse(Packet requestPacket, Func<PacketWrapper, Task> onRecieved, Func<Task> onTimeout = null, int timeout = 5000)
        {
            return client.SendAndGetResponse(new PacketWrapper(requestPacket), onRecieved, onTimeout, timeout);
        }

        public Task Send(Guid id, Packet packet) => Send(new[] { id }, packet);

        public Task Send(Guid[] ids, Packet packet)
        {
            packet.From = StateManager.GetSelfGuid();
            packet.Token = _authToken;
            var forwardedPacket = new ForwardingPacket
            {
                Packet = packet
            };
            forwardedPacket.ForwardToes.AddRange(ids.Select(x => x.ToString()));

            return ForwardToUser(forwardedPacket);
        }

        private Task SendToServer(Packet packet)
        {
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            packet.From = StateManager.GetSelfGuid();
            packet.Token = _authToken;
            return client.Send(new PacketWrapper(packet));
        }

        private Task ForwardToUser(ForwardingPacket forwardingPacket)
        {
            var packet = forwardingPacket.Packet;
            Logger.Debug($"Forwarding data: {LogPacket(packet)}");

            packet.From = StateManager.GetSelfGuid();
            packet.Token = _authToken;
            return SendToServer(new Packet
            {
                ForwardingPacket = forwardingPacket
            });
        }

        static string LogPacket(Packet packet)
        {
            string secondaryInfo = string.Empty;
            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.play_song)
                {
                    var playSong = command.play_song;
                    secondaryInfo = playSong.GameplayParameters.Beatmap.LevelId + " : " +
                                    playSong.GameplayParameters.Beatmap.Difficulty;
                }
                else if (command.TypeCase == Command.TypeOneofCase.load_song)
                {
                    var loadSong = command.load_song;
                    secondaryInfo = loadSong.LevelId;
                }
                else
                {
                    secondaryInfo = command.TypeCase.ToString();
                }
            }

            if (packet.packetCase == Packet.packetOneofCase.Event)
            {
                var @event = packet.Event;

                secondaryInfo = @event.ChangedObjectCase.ToString();
                if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.user_updated)
                {
                    var user = @event.user_updated.User;
                    secondaryInfo =
                        $"{secondaryInfo} from ({user.Name} : {user.DownloadState}) : ({user.PlayState} : {user.StreamDelayMs})";
                }
                else if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.match_updated)
                {
                    var match = @event.match_updated.Match;
                    secondaryInfo = $"{secondaryInfo} ({match.SelectedDifficulty})";
                }
            }

            return $"({packet.packetCase}) ({secondaryInfo})";
        }

        #region State Actions
        public async Task AddUser(string tournamentGuid, User user)
        {
            var @event = new Event
            {
                user_added = new Event.UserAdded
                {
                    TournamentGuid = tournamentGuid,
                    User = user
                }
            };
            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateUser(string tournamentGuid, User user)
        {
            var @event = new Event
            {
                user_updated = new Event.UserUpdated
                {
                    TournamentGuid = tournamentGuid,
                    User = user
                }
            };
            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task RemoveUser(string tournamentGuid, User user)
        {
            var @event = new Event
            {
                user_left = new Event.UserLeft
                {
                    TournamentGuid = tournamentGuid,
                    User = user
                }
            };
            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task CreateMatch(string tournamentGuid, Match match)
        {
            var @event = new Event
            {
                match_created = new Event.MatchCreated
                {
                    TournamentGuid = tournamentGuid,
                    Match = match
                }
            };
            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateMatch(string tournamentGuid, Match match)
        {
            var @event = new Event
            {
                match_updated = new Event.MatchUpdated
                {
                    TournamentGuid = tournamentGuid,
                    Match = match
                }
            };
            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task DeleteMatch(string tournamentGuid, Match match)
        {
            var @event = new Event
            {
                match_deleted = new Event.MatchDeleted
                {
                    TournamentGuid = tournamentGuid,
                    Match = match
                }
            };
            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task CreateQualifierEvent(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                qualifier_created = new Event.QualifierCreated
                {
                    TournamentGuid = tournamentGuid,
                    Event = qualifierEvent
                }
            };

            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateQualifierEvent(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                qualifier_updated = new Event.QualifierUpdated
                {
                    TournamentGuid = tournamentGuid,
                    Event = qualifierEvent
                }
            };

            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task DeleteQualifierEvent(string tournamentGuid, QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                qualifier_deleted = new Event.QualifierDeleted
                {
                    TournamentGuid = tournamentGuid,
                    Event = qualifierEvent
                }
            };
            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task CreateTournament(Tournament tournament)
        {
            var @event = new Event
            {
                tournament_created = new Event.TournamentCreated
                {
                    Tournament = tournament
                }
            };

            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task UpdateTournament(Tournament tournament)
        {
            var @event = new Event
            {
                tournament_updated = new Event.TournamentUpdated
                {
                    Tournament = tournament
                }
            };

            await SendToServer(new Packet
            {
                Event = @event
            });
        }

        public async Task DeleteTournament(Tournament tournament)
        {
            var @event = new Event
            {
                tournament_deleted = new Event.TournamentDeleted
                {
                    Tournament = tournament
                }
            };

            await SendToServer(new Packet
            {
                Event = @event
            });
        }
        #endregion State Actions

        protected virtual async Task Client_PacketWrapperReceived(PacketWrapper packet)
        {
            await Client_PacketReceived(packet.Payload);
        }

        protected virtual async Task Client_PacketReceived(Packet packet)
        {
            Logger.Debug($"Received data: {LogPacket(packet)}");

            //Ready to go, only disabled since it is currently unusued
            /*if (packet.Type != PacketType.Acknowledgement)
            {
                Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id
                }));
            }*/

            /*if (packet.packetCase == Packet.packetOneofCase.Acknowledgement)
            {
                var acknowledgement = packet.Acknowledgement;
            }*/

            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.show_modal)
                {
                    if (ShowModal != null) await ShowModal.Invoke(command.show_modal);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Push)
            {
                var push = packet.Push;
                if (push.DataCase == Push.DataOneofCase.song_finished)
                {
                    if (PlayerFinishedSong != null) await PlayerFinishedSong.Invoke(push.song_finished);
                }
                else if (push.DataCase == Push.DataOneofCase.RealtimeScore)
                {
                    if (RealtimeScoreReceived != null) await RealtimeScoreReceived.Invoke(push.RealtimeScore);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Response)
            {
                var response = packet.Response;
                if (response.DetailsCase == Response.DetailsOneofCase.connect)
                {
                    var connectResponse = response.connect;
                    if (response.Type == Response.ResponseType.Success)
                    {
                        if (ConnectedToServer != null) await ConnectedToServer.Invoke(connectResponse);
                    }
                    else if (response.Type == Response.ResponseType.Fail)
                    {
                        if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(connectResponse);
                    }
                }
                else if (response.DetailsCase == Response.DetailsOneofCase.join)
                {
                    var joinResponse = response.join;
                    if (response.Type == Response.ResponseType.Success)
                    {
                        if (JoinedTournament != null) await JoinedTournament.Invoke(joinResponse);
                    }
                    else if (response.Type == Response.ResponseType.Fail)
                    {
                        if (FailedToJoinTournament != null) await FailedToJoinTournament.Invoke(joinResponse);
                    }
                }
                else if (response.DetailsCase == Response.DetailsOneofCase.image_preloaded)
                {
                    var imagePreloaded = response.image_preloaded;
                    if (ImagePreloaded != null) await ImagePreloaded.Invoke(imagePreloaded, Guid.Parse(packet.From));
                }
            }
        }
    }
}