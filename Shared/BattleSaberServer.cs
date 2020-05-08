using Open.Nat;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BattleSaberShared.Models;
using BattleSaberShared.Models.Packets;
using BattleSaberShared.Sockets;
using static BattleSaberShared.Packet;
using System.Text;
using System.Text.Json;

namespace BattleSaberShared
{
    class BattleSaberServer : IConnection, INotifyPropertyChanged
    {
        Server server;
        Client overlayForwarder;

        public event Action<Player> PlayerConnected;
        public event Action<Player> PlayerDisconnected;
        public event Action<Player> PlayerInfoUpdated;
        public event Action<Match> MatchInfoUpdated;
        public event Action<Match> MatchCreated;
        public event Action<Match> MatchDeleted;

        public event Action<SongFinished> PlayerFinishedSong;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //Tournament State can be modified by ANY client thread, so definitely needs thread-safe accessing
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

        public User Self { get; set; }

        private int port;
        private string serverName;
        private ServerSettings settings;

        public BattleSaberServer()
        {
            var config = new Config("serverConfig.json");

            var portValue = config.GetString("port");
            if (portValue == string.Empty)
            {
                portValue = "10156";
                config.SaveString("port", portValue);
            }

            var nameValue = config.GetString("serverName");
            if (nameValue == string.Empty)
            {
                nameValue = "Default Server Name";
                config.SaveString("serverName", nameValue);
            }

            serverName = nameValue;
            port = int.Parse(portValue);

            settings = new ServerSettings();
            settings.Teams = config.GetTeams();
            settings.TournamentMode = config.GetBoolean("tournamentMode");
        }

        public void Start()
        {
            State = new State();
            State.ServerSettings = settings;
            State.Players = new Player[0];
            State.Coordinators = new MatchCoordinator[0];
            State.Matches = new Match[0];

            Self = new MatchCoordinator()
            {
                Guid = "0",
                Name = "HOST"
            };

            OpenPort();

            server = new Server(port);
            server.PacketRecieved += Server_PacketRecieved;
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
            Task.Run(() => server.Start());

            /*overlayForwarder = new Client("beatsaber.networkauditor.org", 10166);
            Task.Run(() => overlayForwarder.Start());*/
        }

        //Courtesy of andruzzzhka's Multiplayer
        async void OpenPort()
        {
            Logger.Info($"Trying to open port {port} using UPnP...");
            try
            {
                NatDiscoverer discoverer = new NatDiscoverer();
                CancellationTokenSource cts = new CancellationTokenSource(2500);
                NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, ""));

                Logger.Info($"Port {port} is open!");
            }
            catch (Exception)
            {
                Logger.Warning($"Can't open port {port} using UPnP!");
            }
        }

        private void Server_ClientDisconnected(ConnectedClient obj)
        {
            Logger.Debug("Client Disconnected!");

            lock (State)
            {
                if (State.Players.Any(x => x.Guid == obj.guid))
                {
                    var player = State.Players.First(x => x.Guid == obj.guid);
                    RemovePlayer(player);
                }
                else if (State.Coordinators.Any(x => x.Guid == obj.guid))
                {
                    RemoveCoordinator(State.Coordinators.First(x => x.Guid == obj.guid));
                }
            }
        }

        private void Server_ClientConnected(ConnectedClient client)
        {
        }

        public void Send(string guid, Packet packet)
        {
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({(packet.Type == PacketType.Event ? (packet.SpecificPacket as Event).Type.ToString() : "")})");
            server.Send(guid, packet.ToBytes());
        }

        public void Send(string[] guids, Packet packet)
        {
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({(packet.Type == PacketType.Event ? (packet.SpecificPacket as Event).Type.ToString() : "")})");
            server.Send(guids, packet.ToBytes());
        }

        public void SendToOverlay(Packet packet)
        {
            Logger.Debug(packet.GetType().ToString());
            Logger.Debug(packet.SpecificPacket.GetType().ToString());

            //We're assuming the overlay needs JSON, so... Let's convert our serialized class to json
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(packet.SpecificPacket, packet.SpecificPacket.GetType());
            Logger.Debug(Encoding.UTF8.GetString(jsonBytes));
        }

        private void BroadcastToAllClients(Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).Type.ToString();
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).CurrentDownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).CurrentPlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).CurrentScore})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).CurrentlySelectedDifficulty})";
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
            @event.Type = Event.EventType.PlayerAdded;
            @event.ChangedObject = player;
            BroadcastToAllClients(new Packet(@event));

            PlayerConnected?.Invoke(player);
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
            @event.Type = Event.EventType.PlayerUpdated;
            @event.ChangedObject = player;
            BroadcastToAllClients(new Packet(@event));

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
            @event.Type = Event.EventType.PlayerLeft;
            @event.ChangedObject = player;
            BroadcastToAllClients(new Packet(@event));

            PlayerDisconnected?.Invoke(player);
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
            @event.Type = Event.EventType.CoordinatorAdded;
            @event.ChangedObject = coordinator;
            BroadcastToAllClients(new Packet(@event));
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
            @event.Type = Event.EventType.CoordinatorLeft;
            @event.ChangedObject = coordinator;
            BroadcastToAllClients(new Packet(@event));
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
            @event.Type = Event.EventType.MatchCreated;
            @event.ChangedObject = match;
            BroadcastToAllClients(new Packet(@event));

            MatchCreated?.Invoke(match);
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
            @event.Type = Event.EventType.MatchUpdated;
            @event.ChangedObject = match;

            var updatePacket = new Packet(@event);

            BroadcastToAllClients(updatePacket);

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
            @event.Type = Event.EventType.MatchDeleted;
            @event.ChangedObject = match;
            BroadcastToAllClients(new Packet(@event));

            MatchDeleted?.Invoke(match);
        }
        #endregion EventManagement

        private void Server_PacketRecieved(ConnectedClient player, Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).beatmap.levelId + " : " + (packet.SpecificPacket as PlaySong).beatmap.difficulty;
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
                secondaryInfo = (packet.SpecificPacket as Event).Type.ToString();
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).CurrentDownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).CurrentPlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).CurrentScore})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).CurrentlySelectedDifficulty})";
                }
            }
            Logger.Debug($"Recieved: ({packet.Type}) ({secondaryInfo})");
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

                if (connect.clientVersion != SharedConstructs.VersionCode)
                {
                    Send(player.guid, new Packet(new ConnectResponse()
                    {
                        type = ConnectResponse.ResponseType.Fail,
                        self = null,
                        message = $"Version mismatch, this server is on version {SharedConstructs.Version}",
                        serverVersion = SharedConstructs.VersionCode
                    }));
                }
                else if (connect.clientType == Connect.ConnectType.Player)
                {
                    var newPlayer = new Player()
                    {
                        Guid = player.guid,
                        Name = connect.name,
                        UserId = connect.userId,
                        Team = new Team() { Guid = "0", Name = "None"}
                    };

                    AddPlayer(newPlayer);

                    //Give the newly connected player their "self"
                    Send(player.guid, new Packet(new ConnectResponse()
                    {
                        type = ConnectResponse.ResponseType.Success,
                        self = newPlayer,
                        message = $"Connected to {serverName}!",
                        serverVersion = SharedConstructs.VersionCode
                    }));

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
                    Send(player.guid, new Packet(new ConnectResponse()
                    {
                        type = ConnectResponse.ResponseType.Success,
                        self = coordinator,
                        message = $"Connected to {serverName}!",
                        serverVersion = SharedConstructs.VersionCode
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
                switch (@event.Type)
                {
                    case Event.EventType.CoordinatorAdded:
                        AddCoordinator(@event.ChangedObject as MatchCoordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        RemoveCoordinator(@event.ChangedObject as MatchCoordinator);
                        break;
                    case Event.EventType.MatchCreated:
                        CreateMatch(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchUpdated:
                        UpdateMatch(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        DeleteMatch(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        AddPlayer(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerUpdated:
                        UpdatePlayer(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        RemovePlayer(@event.ChangedObject as Player);
                        break;
                    default:
                        Logger.Error($"Unknown command recieved from {player.guid}!");
                        break;
                }
            }
            else if (packet.Type == PacketType.SongFinished)
            {
                BroadcastToAllClients(packet);
                PlayerFinishedSong?.Invoke(packet.SpecificPacket as SongFinished);
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                var forwardingPacket = packet.SpecificPacket as ForwardingPacket;
                var forwardedPacket = new Packet(forwardingPacket.SpecificPacket);
                Send(forwardingPacket.ForwardTo, forwardedPacket);

                //Score updates come through as forwarded packets, so
                if (forwardingPacket.SpecificPacket is Event &&
                    (forwardingPacket.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    SendToOverlay(forwardedPacket);
                }
            }
        }
    }
}
