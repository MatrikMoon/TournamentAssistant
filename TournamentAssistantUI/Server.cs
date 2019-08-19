using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantUI.Models;
using TournamentAssistantUI.Network;
using TournamentAssistantUI.Packets;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI
{
    class Server : IConnection, INotifyPropertyChanged
    {
        Network.Server server;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private TournamentState _state;
        public TournamentState State
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

        public MatchCoordinator Self { get; set; }

        public void Start()
        {
            State = new TournamentState();
            State.Players = new Player[0];
            State.Coordinators = new MatchCoordinator[0];
            State.Matches = new Match[0];

            Self = new MatchCoordinator()
            {
                Guid = "0",
                Name = "HOST"
            };

            var newPlayers = State.Players.ToList();
            newPlayers.Add(new Player()
            {
                Guid = "1",
                Name = "1"
            });
            
            newPlayers.Add(new Player()
            {
                Guid = "2",
                Name = "2"
            });
            State.Players = newPlayers.ToArray();

            NotifyPropertyChanged(nameof(State));

            server = new Network.Server(10155);
            server.PacketRecieved += Server_PacketRecieved;
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
            Task.Run(() => server.Start());
        }

        private void Server_ClientDisconnected(ConnectedClient obj)
        {
            Logger.Debug("Client Disconnected!");

            if (State.Players.Any(x => x.Guid == obj.guid))
            {
                RemovePlayer(State.Players.First(x => x.Guid == obj.guid));
            }
            else if (State.Coordinators.Any(x => x.Guid == obj.guid))
            {
                RemoveCoordinator(State.Coordinators.First(x => x.Guid == obj.guid));
            }
        }

        private void Server_ClientConnected(ConnectedClient obj)
        {
            Logger.Debug("Client Connected!");
        }

        public void Send(string guid, Packet packet)
        {
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({(packet.Type == PacketType.Event ? (packet.SpecificPacket as Event).eventType.ToString() : "")})");
            server.Send(guid, packet.ToBytes());
        }
        public void Send(string[] guids, Packet packet)
        {
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({(packet.Type == PacketType.Event ? (packet.SpecificPacket as Event).eventType.ToString() : "")})");
            server.Send(guids, packet.ToBytes());
        }
        private void BroadcastToCoordinators(Packet packet)
        {
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({(packet.Type == PacketType.Event ? (packet.SpecificPacket as Event).eventType.ToString() :"")})");
            server.Send(State.Coordinators.Select(x => x.Guid).ToArray(), packet.ToBytes());
        }

        public void AddPlayer(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.Add(player);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.PlayerAdded;
            @event.changedObject = player;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void RemovePlayer(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.RemoveAll(x => x.Guid == player.Guid);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.PlayerLeft;
            @event.changedObject = player;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void AddCoordinator(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.Add(coordinator);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.CoordinatorAdded;
            @event.changedObject = coordinator;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void RemoveCoordinator(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.RemoveAll(x => x.Guid == coordinator.Guid);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.CoordinatorLeft;
            @event.changedObject = coordinator;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void CreateMatch(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.Add(match);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.MatchCreated;
            @event.changedObject = match;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void DeleteMatch(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.RemoveAll(x => x.Guid == match.Guid);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.MatchDeleted;
            @event.changedObject = match;
            BroadcastToCoordinators(new Packet(@event));
        }

        private void Server_PacketRecieved(ConnectedClient player, Packet packet)
        {
            if (packet.Type == PacketType.SongList)
            {
                SongList songList = packet.SpecificPacket as SongList;
            }
            else if (packet.Type == PacketType.LoadedSong)
            {
                LoadedSong loadedSong = packet.SpecificPacket as LoadedSong;
            }
            else if (packet.Type == PacketType.Connect)
            {
                Connect connect = packet.SpecificPacket as Connect;

                if (connect.clientType == Connect.ConnectType.Player)
                {
                    AddPlayer(new Player()
                    {
                        Guid = player.guid,
                        Name = connect.name
                    });
                }
                else if (connect.clientType == Connect.ConnectType.Coordinator)
                {
                    var coordinator = new MatchCoordinator()
                    {
                        Guid = player.guid,
                        Name = connect.name
                    };
                    AddCoordinator(coordinator);

                    //Give the newly connected coordinator their "self"
                    Send(player.guid, new Packet(new Event()
                    {
                        eventType = Event.EventType.SetSelf,
                        changedObject = coordinator
                    }));

                    //Give the newly connected coordinator the entire tournament state
                    Send(player.guid, new Packet(State));
                }
            }
            else if (packet.Type == PacketType.TournamentState)
            {
                State = packet.SpecificPacket as TournamentState;
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.eventType)
                {
                    case Event.EventType.CoordinatorAdded:
                        AddCoordinator(@event.changedObject as MatchCoordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        RemoveCoordinator(@event.changedObject as MatchCoordinator);
                        break;
                    case Event.EventType.MatchCreated:
                        CreateMatch(@event.changedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        DeleteMatch(@event.changedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        AddPlayer(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        RemovePlayer(@event.changedObject as Player);
                        break;
                    default:
                        Logger.Error($"Unknown command recieved from {player.guid}!");
                        break;
                }
            }
        }
    }
}
