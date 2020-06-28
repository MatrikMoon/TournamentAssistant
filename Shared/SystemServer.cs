using Open.Nat;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantShared
{
    class SystemServer : IConnection, INotifyPropertyChanged
    {
        Server server;
        Server overlayServer;

        public event Action<Player> PlayerConnected;
        public event Action<Player> PlayerDisconnected;
        public event Action<Player> PlayerInfoUpdated;
        public event Action<Match> MatchInfoUpdated;
        public event Action<Match> MatchCreated;
        public event Action<Match> MatchDeleted;

        public event Action<SongFinished> PlayerFinishedSong;

        public event Action<Acknowledgement, Guid> AckReceived;

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

        //Server settings
        private int port;
        private string serverName;
        private ServerSettings settings;

        //Overlay settings
        private int overlayPort;

        public SystemServer()
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

            var scoreUpdateFrequencyValue = config.GetString("scoreUpdateFrequency");
            if (scoreUpdateFrequencyValue == string.Empty)
            {
                scoreUpdateFrequencyValue = "80";
                config.SaveString("scoreUpdateFrequency", scoreUpdateFrequencyValue);
            }

            serverName = nameValue;
            port = int.Parse(portValue);

            var overlayPortValue = config.GetString("overlayPort");
            if (overlayPortValue == string.Empty || overlayPortValue == "[overlayPort]")
            {
                overlayPortValue = "0";
                config.SaveString("overlayPort", "[overlayPort]");
            }

            overlayPort = int.Parse(overlayPortValue);

            settings = new ServerSettings();
            settings.Teams = config.GetTeams();
            settings.TournamentMode = config.GetBoolean("tournamentMode");
            settings.ScoreUpdateFrequency = Convert.ToInt32(scoreUpdateFrequencyValue);
            settings.BannedMods = config.GetBannedMods();
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
                Id = Guid.Empty,
                Name = "HOST"
            };

            OpenPort(port);

            server = new Server(port);
            server.PacketRecieved += Server_PacketRecieved;
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
            Task.Run(() => server.Start());

            if (overlayPort != 0)
            {
                OpenPort(overlayPort);
                overlayServer = new Server(overlayPort);
                Task.Run(() => overlayServer.Start());
            }
        }

        //Courtesy of andruzzzhka's Multiplayer
        async void OpenPort(int port)
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

        private void Server_ClientDisconnected(ConnectedClient client)
        {
            Logger.Debug("Client Disconnected!");

            lock (State)
            {
                if (State.Players.Any(x => x.Id == client.id))
                {
                    var player = State.Players.First(x => x.Id == client.id);
                    RemovePlayer(player);
                }
                else if (State.Coordinators.Any(x => x.Id == client.id))
                {
                    RemoveCoordinator(State.Coordinators.First(x => x.Id == client.id));
                }
            }
        }

        private void Server_ClientConnected(ConnectedClient client)
        {
        }

        public void Send(Guid id, Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).Beatmap.LevelId + " : " + (packet.SpecificPacket as PlaySong).Beatmap.Difficulty;
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
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).DownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).PlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).Score} : {((packet.SpecificPacket as Event).ChangedObject as Player).StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).SelectedDifficulty})";
                }
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }

            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo}) TO ({id})");
            #endregion LOGGING

            packet.From = Self?.Id ?? Guid.Empty;
            server.Send(id, packet.ToBytes());
        }

        public void Send(Guid[] ids, Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).Beatmap.LevelId + " : " + (packet.SpecificPacket as PlaySong).Beatmap.Difficulty;
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
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).DownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).PlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).Score} : {((packet.SpecificPacket as Event).ChangedObject as Player).StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).SelectedDifficulty})";
                }
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }

            var toIds = string.Empty;
            foreach (var id in ids) toIds += $"{id}, ";
            toIds = toIds.Substring(0, toIds.Length - 2);

            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo}) TO ({toIds})");
            #endregion LOGGING

            packet.From = Self?.Id ?? Guid.Empty;
            server.Send(ids, packet.ToBytes());
        }

        public void ForwardTo(Guid[] ids, Guid from, Packet packet)
        {
            packet.From = from;

            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).Beatmap.LevelId + " : " + (packet.SpecificPacket as PlaySong).Beatmap.Difficulty;
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
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).DownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).PlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).Score} : {((packet.SpecificPacket as Event).ChangedObject as Player).StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).SelectedDifficulty})";
                }
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }

            var toIds = string.Empty;
            foreach (var id in ids) toIds += $"{id}, ";
            toIds = toIds.Substring(0, toIds.Length - 2);

            Logger.Debug($"Forwarding {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo}) TO ({toIds}) FROM ({packet.From})");
            #endregion LOGGING

            server.Send(ids, packet.ToBytes());
        }

        public void SendToOverlay(Packet packet)
        {
            if (overlayServer != null)
            {
                //We're assuming the overlay needs JSON, so... Let's convert our serialized class to json
                var jsonString = JsonSerializer.Serialize(packet, packet.GetType());
                Logger.Debug(jsonString);

                Task.Run(() =>
                {
                    try
                    {
                        overlayServer.Broadcast(Encoding.UTF8.GetBytes(jsonString + @"{\uwu/}"));
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error sending to overlay:");
                        Logger.Error(e.Message);
                    }
                });
            }
        }

        private void BroadcastToAllClients(Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.PlaySong)
            {
                secondaryInfo = (packet.SpecificPacket as PlaySong).Beatmap.LevelId + " : " + (packet.SpecificPacket as PlaySong).Beatmap.Difficulty;
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
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).DownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).PlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).Score} : {((packet.SpecificPacket as Event).ChangedObject as Player).StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).SelectedDifficulty})";
                }
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo})");
            #endregion LOGGING

            packet.From = Self.Id;
            server.Broadcast(packet.ToBytes());
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
                newPlayers[newPlayers.FindIndex(x => x.Id == player.Id)] = player;
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
                newPlayers.RemoveAll(x => x.Id == player.Id);
                State.Players = newPlayers.ToArray();

                //IN-TESTING
                //Remove the player from any matches they were in
                /*var match = State.Matches.FirstOrDefault(x => x.Players.Contains(player));
                if (match != null)
                {
                    match.Players = match.Players.Where(x => x != player).ToArray();
                    UpdateMatch(match);
                }*/
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
                newCoordinators.RemoveAll(x => x.Id == coordinator.Id);
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
                secondaryInfo = (packet.SpecificPacket as PlaySong).Beatmap.LevelId + " : " + (packet.SpecificPacket as PlaySong).Beatmap.Difficulty;
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
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).DownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).PlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).Score} : {((packet.SpecificPacket as Event).ChangedObject as Player).StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).SelectedDifficulty})";
                }
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }
            Logger.Debug($"Recieved {packet.ToBytes().Length} bytes: ({packet.Type}) ({secondaryInfo})");
            #endregion LOGGING

            SendToOverlay(packet);

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
            else if (packet.Type == PacketType.SongList)
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

                if (connect.ClientVersion != SharedConstructs.VersionCode)
                {
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Type = ConnectResponse.ResponseType.Fail,
                        Self = null,
                        State = null,
                        Message = $"Version mismatch, this server is on version {SharedConstructs.Version}",
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
                else if (connect.ClientType == Connect.ConnectTypes.Player)
                {
                    var newPlayer = new Player()
                    {
                        Id = player.id,
                        Name = connect.Name,
                        UserId = connect.UserId,
                        Team = new Team() { Id = Guid.Empty, Name = "None"}
                    };

                    AddPlayer(newPlayer);

                    //Give the newly connected player their Self and State
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Type = ConnectResponse.ResponseType.Success,
                        Self = newPlayer,
                        State = State,
                        Message = $"Connected to {serverName}!",
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
                else if (connect.ClientType == Connect.ConnectTypes.Coordinator)
                {
                    var coordinator = new MatchCoordinator()
                    {
                        Id = player.id,
                        Name = connect.Name
                    };
                    AddCoordinator(coordinator);

                    //Give the newly connected coordinator their Self and State
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Type = ConnectResponse.ResponseType.Success,
                        Self = coordinator,
                        State = State,
                        Message = $"Connected to {serverName}!",
                        ServerVersion = SharedConstructs.VersionCode
                    }));
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
                        Logger.Error($"Unknown command recieved from {player.id}!");
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
                ForwardTo(forwardingPacket.ForwardTo, packet.From, forwardedPacket);
            }
        }
    }
}
