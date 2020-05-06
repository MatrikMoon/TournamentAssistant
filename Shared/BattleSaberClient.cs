using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using BattleSaberShared.Models;
using BattleSaberShared.Models.Packets;
using static BattleSaberShared.Models.Packets.Connect;
using static BattleSaberShared.Packet;

namespace BattleSaberShared
{
    public class BattleSaberClient : IConnection, INotifyPropertyChanged
    {
        public event Action<Player> PlayerConnected;
        public event Action<Player> PlayerDisconnected;
        public event Action<Player> PlayerInfoUpdated;
        public event Action<Match> MatchInfoUpdated;
        public event Action<Match> MatchCreated;
        public event Action<Match> MatchDeleted;

        public event Action<SongFinished> PlayerFinishedSong;

        public event Action<State> StateUpdated;
        public event Action<ConnectResponse> ConnectedToServer;
        public event Action<ConnectResponse> FailedToConnectToServer;
        public event Action ServerDisconnected;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //Tournament State in the client can ONLY be modified by the server connection thread, so thread-safety shouldn't be an issue here
        private State _state;
        public State State {
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

        protected Sockets.Client client;

        public bool Connected => client?.Connected ?? false;

        private Timer heartbeatTimer = new Timer();
        private string endpoint;
        private int port;
        private string username;
        private ConnectType connectType;

        public BattleSaberClient(string endpoint, int port, string username, ConnectType connectType)
        {
            this.endpoint = endpoint;
            this.port = port;
            this.username = username;
            this.connectType = connectType;
        }

        public void Start()
        {
            heartbeatTimer.Interval = 10000;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;

            ConnectToServer();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            try
            {
                var command = new Command();
                command.commandType = Command.CommandType.Heartbeat;
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
                State.Players = new Player[0];
                State.Coordinators = new MatchCoordinator[0];
                State.Matches = new Match[0];

                client = new Sockets.Client(endpoint, port);
                client.PacketRecieved += Client_PacketRecieved;
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
            heartbeatTimer.Start();

            Send(new Packet(new Connect()
            {
                clientType = connectType,
                name = username,
                clientVersion = SharedConstructs.VersionCode
            }));
        }

        private void Client_ServerFailedToConnect()
        {
            //Resume heartbeat if we fail to connect
            //Basically the same as just doing another connect here...
            //But with some extra delay. I don't really know why
            //I'm doing it this way
            heartbeatTimer.Start();

            FailedToConnectToServer?.Invoke(null);
        }

        private void Client_ServerDisconnected()
        {
            Logger.Debug("Server disconnected!");
            ServerDisconnected?.Invoke();
        }

        public void Shutdown()
        {
            if (client.Connected) client.Shutdown();
            heartbeatTimer.Stop();
        }

        protected virtual void Client_PacketRecieved(Packet packet)
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

            if (packet.Type == PacketType.State)
            {
                State = packet.SpecificPacket as State;
                StateUpdated?.Invoke(State);
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.Type)
                {
                    case Event.EventType.CoordinatorAdded:
                        AddCoordinatorRecieved(@event.ChangedObject as MatchCoordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        RemoveCoordinatorRecieved(@event.ChangedObject as MatchCoordinator);
                        break;
                    case Event.EventType.MatchCreated:
                        AddMatchRecieved(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchUpdated:
                        UpdateMatchRecieved(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        DeleteMatchRecieved(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        AddPlayerRecieved(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerUpdated:
                        UpdatePlayerRecieved(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        RemovePlayerRecieved(@event.ChangedObject as Player);
                        break;
                    default:
                        Logger.Error($"Unknown command recieved!");
                        break;
                }
            }
            else if (packet.Type == PacketType.ConnectResponse)
            {
                var response = packet.SpecificPacket as ConnectResponse;
                if (response.type == ConnectResponse.ResponseType.Success)
                {
                    Self = response.self;
                    ConnectedToServer?.Invoke(response);
                }
                else if (response.type == ConnectResponse.ResponseType.Fail)
                {
                    FailedToConnectToServer?.Invoke(response);
                }
            }
            else if (packet.Type == PacketType.SongFinished)
            {
                PlayerFinishedSong?.Invoke(packet.SpecificPacket as SongFinished);
            }
        }

        public void Send(string guid, Packet packet) => Send(new string[] { guid }, packet);

        public void Send(string[] guids, Packet packet)
        {
            var forwardedPacket = new ForwardingPacket();
            forwardedPacket.ForwardTo = guids;
            forwardedPacket.Type = packet.Type;
            forwardedPacket.SpecificPacket = packet.SpecificPacket;

            Send(new Packet(forwardedPacket));
        }

        public void Send(Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).Type.ToString();
                if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).CurrentlySelectedDifficulty})";
                }
            }
            else if (packet.Type == PacketType.Command)
            {
                secondaryInfo = (packet.SpecificPacket as Command).commandType.ToString();
            }

            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo})");
            #endregion LOGGING

            client.Send(packet.ToBytes());
        }

        #region EVENTS/ACTIONS
        public void AddPlayer(Player player)
        {
            var @event = new Event();
            @event.Type = Event.EventType.PlayerAdded;
            @event.ChangedObject = player;
            Send(new Packet(@event));
        }

        private void AddPlayerRecieved(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.Add(player);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            PlayerConnected?.Invoke(player);
        }

        public void UpdatePlayer(Player player)
        {
            var @event = new Event();
            @event.Type = Event.EventType.PlayerUpdated;
            @event.ChangedObject = player;
            Send(new Packet(@event));
        }

        public void UpdatePlayerRecieved(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers[newPlayers.FindIndex(x => x.Guid == player.Guid)] = player;
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            PlayerInfoUpdated?.Invoke(player);
        }

        public void RemovePlayer(Player player)
        {
            var @event = new Event();
            @event.Type = Event.EventType.PlayerLeft;
            @event.ChangedObject = player;
            Send(new Packet(@event));
        }

        private void RemovePlayerRecieved(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.RemoveAll(x => x.Guid == player.Guid);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            PlayerDisconnected?.Invoke(player);
        }

        public void AddCoordinator(MatchCoordinator coordinator)
        {
            var @event = new Event();
            @event.Type = Event.EventType.CoordinatorAdded;
            @event.ChangedObject = coordinator;
            Send(new Packet(@event));
        }

        private void AddCoordinatorRecieved(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.Add(coordinator);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void RemoveCoordinator(MatchCoordinator coordinator)
        {
            var @event = new Event();
            @event.Type = Event.EventType.CoordinatorLeft;
            @event.ChangedObject = coordinator;
            Send(new Packet(@event));
        }

        private void RemoveCoordinatorRecieved(MatchCoordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.RemoveAll(x => x.Guid == coordinator.Guid);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void CreateMatch(Match match)
        {
            var @event = new Event();
            @event.Type = Event.EventType.MatchCreated;
            @event.ChangedObject = match;
            Send(new Packet(@event));
        }

        private void AddMatchRecieved(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.Add(match);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            MatchCreated?.Invoke(match);
        }

        public void UpdateMatch(Match match)
        {
            var @event = new Event();
            @event.Type = Event.EventType.MatchUpdated;
            @event.ChangedObject = match;
            Send(new Packet(@event));
        }

        public void UpdateMatchRecieved(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches[newMatches.FindIndex(x => x.Guid == match.Guid)] = match;
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            MatchInfoUpdated?.Invoke(match);
        }

        public void DeleteMatch(Match match)
        {
            var @event = new Event();
            @event.Type = Event.EventType.MatchDeleted;
            @event.ChangedObject = match;
            Send(new Packet(@event));
        }

        private void DeleteMatchRecieved(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.RemoveAll(x => x.Guid == match.Guid);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            MatchDeleted?.Invoke(match);
        }
        #endregion EVENTS/ACTIONS
    }
}
