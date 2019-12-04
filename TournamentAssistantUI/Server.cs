using Open.Nat;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.Network;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI
{
    class Server : IConnection, INotifyPropertyChanged
    {
        Network.Server server;

        public event Action<Player> PlayerInfoUpdated;
        public event Action<Player> PlayerFinishedSong;
        public event Action<Match> MatchInfoUpdated;
        public event Action<Match> MatchDeleted;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //Tournament State can be modified by ANY client thread, so definitely needs thread-safe accessing
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

            OpenPort();

            server = new Network.Server(10156);
            server.PacketRecieved += Server_PacketRecieved;
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
            Task.Run(() => server.Start());
        }

        //Courtesy of andruzzzhka's Multiplayer
        async static void OpenPort()
        {
            Logger.Info($"Trying to open port {10156} using UPnP...");
            try
            {
                NatDiscoverer discoverer = new NatDiscoverer();
                CancellationTokenSource cts = new CancellationTokenSource(2500);
                NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 10156, 10156, ""));

                Logger.Info($"Port {10156} is open!");
            }
            catch (Exception)
            {
                Logger.Info($"Can't open port {10156} using UPnP!");
            }
        }

        private void Server_ClientDisconnected(ConnectedClient obj)
        {
            Logger.Debug("Client Disconnected!");

            lock (State)
            {
                if (State.Players.Any(x => x.Guid == obj.guid))
                {
                    RemovePlayer(State.Players.First(x => x.Guid == obj.guid));
                }
                else if (State.Coordinators.Any(x => x.Guid == obj.guid))
                {
                    RemoveCoordinator(State.Coordinators.First(x => x.Guid == obj.guid));
                }
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
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).eventType.ToString();
                if ((packet.SpecificPacket as Event).eventType == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).changedObject as Player).Name} : {((packet.SpecificPacket as Event).changedObject as Player).CurrentDownloadState}) : ({((packet.SpecificPacket as Event).changedObject as Player).CurrentPlayState} : {((packet.SpecificPacket as Event).changedObject as Player).CurrentScore})";
                }
                else if ((packet.SpecificPacket as Event).eventType == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).changedObject as Match).CurrentlySelectedDifficulty})";
                }
            }

            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo})");
            #endregion LOGGING

            string[] coordinators = null;
            lock (State)
            {
                coordinators = State.Coordinators.Select(x => x.Guid).Union(State.Players.Select(x => x.Guid)).ToArray();
            }

            server.Send(coordinators, packet.ToBytes());
        }

        #region EventManagement
        public void AddPlayer(Player player)
        {
            lock (State)
            {
                var newPlayers = State.Players.ToList();
                newPlayers.Add(player);
                State.Players = newPlayers.ToArray();
            }
            
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.PlayerAdded;
            @event.changedObject = player;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void UpdatePlayer(Player player)
        {
            lock (State)
            {
                var newPlayers = State.Players.ToList();
                newPlayers[newPlayers.FindIndex(x => x.Guid == player.Guid)] = player;
                State.Players = newPlayers.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.PlayerUpdated;
            @event.changedObject = player;
            BroadcastToCoordinators(new Packet(@event));

            PlayerInfoUpdated?.Invoke(player);
        }

        public void RemovePlayer(Player player)
        {
            lock (State)
            {
                var newPlayers = State.Players.ToList();
                newPlayers.RemoveAll(x => x.Guid == player.Guid);
                State.Players = newPlayers.ToArray();
            }
            
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.PlayerLeft;
            @event.changedObject = player;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void AddCoordinator(MatchCoordinator coordinator)
        {
            lock (State)
            {
                var newCoordinators = State.Coordinators.ToList();
                newCoordinators.Add(coordinator);
                State.Coordinators = newCoordinators.ToArray();
            }
            
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.CoordinatorAdded;
            @event.changedObject = coordinator;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void RemoveCoordinator(MatchCoordinator coordinator)
        {
            lock (State)
            {
                var newCoordinators = State.Coordinators.ToList();
                newCoordinators.RemoveAll(x => x.Guid == coordinator.Guid);
                State.Coordinators = newCoordinators.ToArray();
            }
            
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.CoordinatorLeft;
            @event.changedObject = coordinator;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void CreateMatch(Match match)
        {
            lock (State)
            {
                var newMatches = State.Matches.ToList();
                newMatches.Add(match);
                State.Matches = newMatches.ToArray();
            }
            
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.MatchCreated;
            @event.changedObject = match;
            BroadcastToCoordinators(new Packet(@event));
        }

        public void UpdateMatch(Match match)
        {
            lock (State)
            {
                var newMatches = State.Matches.ToList();
                newMatches[newMatches.FindIndex(x => x.Guid == match.Guid)] = match;
                State.Matches = newMatches.ToArray();
            }
            
            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.MatchUpdated;
            @event.changedObject = match;

            var updatePacket = new Packet(@event);

            BroadcastToCoordinators(updatePacket);

            MatchInfoUpdated?.Invoke(match);
        }

        public void DeleteMatch(Match match)
        {
            lock (State)
            {
                var newMatches = State.Matches.ToList();
                newMatches.RemoveAll(x => x.Guid == match.Guid);
                State.Matches = newMatches.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.eventType = Event.EventType.MatchDeleted;
            @event.changedObject = match;
            BroadcastToCoordinators(new Packet(@event));

            MatchDeleted?.Invoke(match);
        }
        #endregion EventManagement

        private void Server_PacketRecieved(ConnectedClient player, Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).levelId + " : " + (packet.SpecificPacket as PlaySong).difficulty;
            }
            else if (packet.Type == PacketType.LoadSong)
            {
                secondaryInfo = (packet.SpecificPacket as LoadSong).levelId;
            }
            else if (packet.Type == PacketType.Command)
            {
                secondaryInfo = (packet.SpecificPacket as Command).commandType.ToString();
            }
            else if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).eventType.ToString();
                if ((packet.SpecificPacket as Event).eventType == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).changedObject as Player).Name} : {((packet.SpecificPacket as Event).changedObject as Player).CurrentDownloadState}) : ({((packet.SpecificPacket as Event).changedObject as Player).CurrentPlayState} : {((packet.SpecificPacket as Event).changedObject as Player).CurrentScore})";
                }
                else if ((packet.SpecificPacket as Event).eventType == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).changedObject as Match).CurrentlySelectedDifficulty})";
                }
            }
            Logger.Info($"Recieved: ({packet.Type}) ({secondaryInfo})");
            #endregion LOGGING

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
                    string guid;
                    if (connect.name.StartsWith("TEST"))
                    {
                        guid = "test";
                    }
                    else guid = player.guid;

                    var newPlayer = new Player()
                    {
                        Guid = guid,
                        Name = connect.name
                    };

                    AddPlayer(newPlayer);

                    //Give the newly connected player their "self"
                    Send(player.guid, new Packet(new Event()
                    {
                        eventType = Event.EventType.SetSelf,
                        changedObject = newPlayer
                    }));

                    //In-testing: I'm trying out sending the entire state to players as well so they can see what's going on in other matches...
                    //There's a chance this could cause too much load on the server, but we'll see how it goes
                    lock (State)
                    {
                        Send(player.guid, new Packet(State));
                    }
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
                    lock (State)
                    {
                        Send(player.guid, new Packet(State));
                    }
                }
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
                    case Event.EventType.MatchUpdated:
                        UpdateMatch(@event.changedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        DeleteMatch(@event.changedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        AddPlayer(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerUpdated:
                        UpdatePlayer(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        RemovePlayer(@event.changedObject as Player);
                        break;
                    case Event.EventType.PlayerFinishedSong:
                        UpdatePlayer(@event.changedObject as Player); //PlayerFinishedSong contains an updated Player with the final scores
                        BroadcastToCoordinators(packet);
                        PlayerFinishedSong?.Invoke(@event.changedObject as Player);
                        break;
                    default:
                        Logger.Error($"Unknown command recieved from {player.guid}!");
                        break;
                }
            }
            else if (packet.Type == PacketType.ForwardedPacket)
            {
                var forwardedPacket = packet.SpecificPacket as ForwardedPacket;
                Send(forwardedPacket.ForwardTo, new Packet(forwardedPacket.SpecificPacket));
            }
        }
    }
}
