using Discord;
using Google.Protobuf;
using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Models.Packets.Connect.Types;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantShared
{
    public class SystemClient : IConnection, INotifyPropertyChanged
    {
        public event Action<Player> PlayerConnected;

        public event Action<Player> PlayerDisconnected;

        public event Action<Player> PlayerInfoUpdated;

        public event Action<Match> MatchInfoUpdated;

        public event Action<Match> MatchCreated;

        public event Action<Match> MatchDeleted;

        public event Action<Acknowledgement, Guid> AckReceived;

        public event Action<SongFinished> PlayerFinishedSong;

        public event Action<ConnectResponse> ConnectedToServer;

        public event Action<ConnectResponse> FailedToConnectToServer;

        public event Action ServerDisconnected;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //Tournament State in the client *should* only be modified by the server connection thread, so thread-safety shouldn't be an issue here
        private State _state;

        public State State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                NotifyPropertyChanged(nameof(State));
            }
        }

        public User Self { get; private set; }
        public object SelfObject { get; private set; }

        protected Sockets.Client client;

        public bool Connected => client?.Connected ?? false;

        private Timer heartbeatTimer = new Timer();
        private bool shouldHeartbeat;
        private string endpoint;
        private int port;
        private string username;
        private string password;
        private string userId;
        private ConnectTypes connectType;

        public SystemClient(string endpoint, int port, string username, ConnectTypes connectType, string userId = "0", string password = "")
        {
            this.endpoint = endpoint;
            this.port = port;
            this.username = username;
            this.password = password;
            this.userId = userId;
            this.connectType = connectType;
        }

        public void Start()
        {
            shouldHeartbeat = true;
            heartbeatTimer.Interval = 10000;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;

            ConnectToServer();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            try
            {
                var command = new Command
                {
                    CommandType = Command.Types.CommandTypes.Heartbeat
                };
                Send(new Packet(command));
            }
            catch (Exception e)
            {
                Logger.Debug("HEARTBEAT FAILED");
                Logger.Debug(e.ToString());

                ConnectToServer();
            }
        }

        private void ConnectToServer()
        {
            //Don't heartbeat while connecting
            heartbeatTimer.Stop();

            try
            {
                State = new State();

                client = new Sockets.Client(endpoint, port);
                client.PacketReceived += Client_PacketReceived;
                client.ServerConnected += Client_ServerConnected;
                client.ServerFailedToConnect += Client_ServerFailedToConnect;
                client.ServerDisconnected += Client_ServerDisconnected;

                client.Start();
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to connect to server. Retrying...");
                Logger.Debug(e.ToString());
            }
        }

        private void Client_ServerConnected()
        {
            //Resume heartbeat when connected
            if (shouldHeartbeat) heartbeatTimer.Start();

            Send(new Packet(new Connect()
            {
                ClientType = connectType,
                Name = username,
                Password = password,
                UserId = userId,
                ClientVersion = SharedConstructs.VersionCode
            }));
        }

        private void Client_ServerFailedToConnect()
        {
            //Resume heartbeat if we fail to connect
            //Basically the same as just doing another connect here...
            //But with some extra delay. I don't really know why
            //I'm doing it this way
            if (shouldHeartbeat) heartbeatTimer.Start();

            FailedToConnectToServer?.Invoke(null);
        }

        private void Client_ServerDisconnected()
        {
            Logger.Debug("Server disconnected!");
            ServerDisconnected?.Invoke();
        }

        public void Shutdown()
        {
            client?.Shutdown();
            heartbeatTimer.Stop();

            //If the client was connecting when we shut it down, the FailedToConnect event might resurrect the heartbeat without this
            shouldHeartbeat = false;
        }

        public void Send(Guid id, Packet packet) => Send(new Guid[] { id }, packet);

        public void Send(Guid[] ids, Packet packet)
        {
            packet.From = Guid.TryParse(Self?.Id, out var g) ? g : Guid.Empty;

            var forwardedPacket = new ForwardingPacket
            {
                Type = packet.Type,
                SpecificPacket = Google.Protobuf.WellKnownTypes.Any.Pack(packet.SpecificPacket as Google.Protobuf.IMessage)
            };
            forwardedPacket.ForwardTo.AddRange(ids.Select(g => g.ToString()));

            Send(new Packet(forwardedPacket));
        }

        private static void Log(Packet packet)
        {
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).GameplayParameters.Beatmap.LevelId + " : " + (packet.SpecificPacket as PlaySong).GameplayParameters.Beatmap.Difficulty;
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                secondaryInfo = (packet.SpecificPacket as LoadSong).LevelId;
            }
            else if (packet.Type == PacketType.Command)
            {
                secondaryInfo = (packet.SpecificPacket as Command).CommandType.ToString();
            }
            else if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).Type.ToString();
                if ((packet.SpecificPacket as Event).Type == Event.Types.EventType.PlayerUpdated)
                {
                    var p = (packet.SpecificPacket as Event).ChangedObject.Unpack<Player>();
                    secondaryInfo = $"{secondaryInfo} from ({p.Name} : {p.DownloadState}) : ({p.PlayState} : {p.Score} : {p.StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.Types.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject.Unpack<Match>()).SelectedDifficulty})";
                }
            }
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes: ({packet.Type}) ({secondaryInfo})");
        }

        public IAsyncResult Send(Packet packet)
        {
            packet.From = Guid.TryParse(Self?.Id, out var g) ? g : Guid.Empty;
            return client.Send(packet.ToBytes());
        }

        #region EVENTS/ACTIONS

        public void AddPlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerAdded,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(player)
            };
            Send(new Packet(@event));
        }

        private void AddPlayerReceived(Player player)
        {
            State.Players.Add(player);
            NotifyPropertyChanged(nameof(State));

            PlayerConnected?.Invoke(player);
        }

        public void UpdatePlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerUpdated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(player)
            };
            Send(new Packet(@event));
        }

        public void UpdatePlayerReceived(Player player)
        {
            // TODO: This is garbage
            var newPlayers = State.Players.ToList();
            newPlayers[newPlayers.FindIndex(x => x.Id == player.Id)] = player;
            State.Players.Clear();
            State.Players.AddRange(newPlayers);
            NotifyPropertyChanged(nameof(State));

            //If the player updated is *us* (an example of this coming from the outside is stream sync info)
            //we should update our Self

            // TODO: This shouldn't be a new User call
            if (Self.Id == player.Id)
            {
                SelfObject = player;
                Self = new User
                {
                    Id = player.Id,
                    Name = player.Name
                };
            }

            PlayerInfoUpdated?.Invoke(player);
        }

        public void RemovePlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerLeft,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(player)
            };
            Send(new Packet(@event));
        }

        private void RemovePlayerReceived(Player player)
        {
            // TODO: This is garbage
            var newPlayers = State.Players.ToList();
            newPlayers.RemoveAll(x => x.Id == player.Id);
            State.Players.Clear();
            State.Players.AddRange(newPlayers);
            NotifyPropertyChanged(nameof(State));

            PlayerDisconnected?.Invoke(player);
        }

        public void AddCoordinator(Coordinator coordinator)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.CoordinatorAdded,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(coordinator)
            };
            Send(new Packet(@event));
        }

        private void AddCoordinatorReceived(Coordinator coordinator)
        {
            State.Coordinators.Add(coordinator);
            NotifyPropertyChanged(nameof(State));
        }

        public void RemoveCoordinator(Coordinator coordinator)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.CoordinatorLeft,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(coordinator)
            };
            Send(new Packet(@event));
        }

        private void RemoveCoordinatorReceived(Coordinator coordinator)
        {
            // TODO: This is garbage
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.RemoveAll(x => x.Id == coordinator.Id);
            State.Coordinators.Clear();
            State.Coordinators.AddRange(newCoordinators);
            NotifyPropertyChanged(nameof(State));
        }

        public void CreateMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.MatchCreated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(match)
            };
            Send(new Packet(@event));
        }

        private void AddMatchReceived(Match match)
        {
            State.Matches.Add(match);
            NotifyPropertyChanged(nameof(State));

            MatchCreated?.Invoke(match);
        }

        public void UpdateMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.MatchUpdated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(match)
            };
            Send(new Packet(@event));
        }

        public void UpdateMatchReceived(Match match)
        {
            // TODO: This is garbage
            var newMatches = State.Matches.ToList();
            newMatches[newMatches.FindIndex(x => x.Guid == match.Guid)] = match;
            State.Matches.Clear();
            State.Matches.AddRange(newMatches);
            NotifyPropertyChanged(nameof(State));

            MatchInfoUpdated?.Invoke(match);
        }

        public void DeleteMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.MatchDeleted,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(match)
            };
            Send(new Packet(@event));
        }

        private void DeleteMatchReceived(Match match)
        {
            // TODO: This is garbage
            var newMatches = State.Matches.ToList();
            newMatches.RemoveAll(x => x.Guid == match.Guid);
            State.Matches.Clear();
            State.Matches.AddRange(newMatches);
            NotifyPropertyChanged(nameof(State));

            MatchDeleted?.Invoke(match);
        }

        private void AddQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            State.Events.Add(qualifierEvent);
            NotifyPropertyChanged(nameof(State));
            // TODO: This should probably send something back, no?
        }

        public void UpdateQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.QualifierEventUpdated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
            };
            Send(new Packet(@event));
        }

        public void UpdateQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            // TODO: This is garbage
            var newEvents = State.Events.ToList();
            newEvents[newEvents.FindIndex(x => x.EventId == qualifierEvent.EventId)] = qualifierEvent;
            State.Events.Clear();
            State.Events.AddRange(newEvents);
            NotifyPropertyChanged(nameof(State));
        }

        public void DeleteQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.QualifierEventDeleted,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
            };
            Send(new Packet(@event));
        }

        private void DeleteQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            // TODO: This is garbage
            var newEvents = State.Events.ToList();
            newEvents.RemoveAll(x => x.EventId == qualifierEvent.EventId);
            State.Events.AddRange(newEvents);
            NotifyPropertyChanged(nameof(State));
        }

        #endregion EVENTS/ACTIONS

        protected virtual void Client_PacketReceived(Packet packet)
        {
            #region LOGGING

            Log(packet);

            #endregion LOGGING

            //Ready to go, only disabled since it is currently unusued
            /*if (packet.Type != PacketType.Acknowledgement)
            {
                Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id
                }));
            }*/

            if (packet.Type == PacketType.Acknowledgement)
            {
                Acknowledgement acknowledgement = packet.SpecificPacket as Acknowledgement;
                AckReceived?.Invoke(acknowledgement, packet.From);
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.Type)
                {
                    case Event.Types.EventType.CoordinatorAdded:
                        AddCoordinatorReceived(@event.ChangedObject.Unpack<Coordinator>());
                        break;

                    case Event.Types.EventType.CoordinatorLeft:
                        RemoveCoordinatorReceived(@event.ChangedObject.Unpack<Coordinator>());
                        break;

                    case Event.Types.EventType.MatchCreated:
                        AddMatchReceived(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.MatchUpdated:
                        UpdateMatchReceived(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.MatchDeleted:
                        DeleteMatchReceived(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.PlayerAdded:
                        AddPlayerReceived(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.PlayerUpdated:
                        UpdatePlayerReceived(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.PlayerLeft:
                        RemovePlayerReceived(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.QualifierEventCreated:
                        AddQualifierEventReceived(@event.ChangedObject.Unpack<QualifierEvent>());
                        break;

                    case Event.Types.EventType.QualifierEventUpdated:
                        UpdateQualifierEventReceived(@event.ChangedObject.Unpack<QualifierEvent>());
                        break;

                    case Event.Types.EventType.QualifierEventDeleted:
                        DeleteQualifierEventReceived(@event.ChangedObject.Unpack<QualifierEvent>());
                        break;

                    case Event.Types.EventType.HostAdded:
                        break;

                    case Event.Types.EventType.HostRemoved:
                        break;

                    default:
                        Logger.Error($"Unknown command received!");
                        break;
                }
            }
            else if (packet.Type == PacketType.ConnectResponse)
            {
                var response = packet.SpecificPacket as ConnectResponse;
                if (response.Response.Type == Response.Types.ResponseType.Success)
                {
                    switch (response.UserCase)
                    {
                        case ConnectResponse.UserOneofCase.Coordinator:
                            SelfObject = response.Coordinator;
                            Self = new User
                            {
                                Id = response.Coordinator.Id,
                                Name = response.Coordinator.Name
                            };
                            break;

                        case ConnectResponse.UserOneofCase.Player:
                            SelfObject = response.Player;
                            Self = new User
                            {
                                Id = response.Player.Id,
                                Name = response.Player.Name
                            };
                            break;
                    }
                    State = response.State;
                    ConnectedToServer?.Invoke(response);
                }
                else if (response.Response.Type == Response.Types.ResponseType.Fail)
                {
                    FailedToConnectToServer?.Invoke(response);
                }
            }
            else if (packet.Type == PacketType.SongFinished)
            {
                PlayerFinishedSong?.Invoke(packet.SpecificPacket as SongFinished);
            }
        }
    }
}