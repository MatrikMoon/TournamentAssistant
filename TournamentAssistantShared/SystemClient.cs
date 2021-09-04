using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Models.Packets.Connect;
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

        public User Self { get; set; }

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

        public SystemClient(string endpoint, int port, string username, ConnectTypes connectType, string userId = "0", string? password = null)
        {
            SetConnectionDetails(endpoint, port, username, connectType, userId, password);
        }

        public SystemClient()
        {
        }

        public void SetConnectionDetails(string endpoint, int port, string username, ConnectTypes connectType, string userId = "0", string? password = null)
        {
            if (Connected)
                return;

            this.endpoint = endpoint;
            this.port = port;
            this.username = username;
            this.password = password;
            this.userId = userId;
            this.connectType = connectType;
        }

        public void Start()
        {
            if (endpoint == null)
                throw new Exception("Details have not been set.");

            shouldHeartbeat = true;
            heartbeatTimer.Interval = 10000;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;

            ConnectToServer();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            try
            {
                var command = new Command();
                command.CommandType = Command.CommandTypes.Heartbeat;
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
                State.Coordinators = new Coordinator[0];
                State.Matches = new Match[0];

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
            packet.From = Self?.Id ?? Guid.Empty;

            var forwardedPacket = new ForwardingPacket();
            forwardedPacket.ForwardTo = ids;
            forwardedPacket.Type = packet.Type;
            forwardedPacket.SpecificPacket = packet.SpecificPacket;

            Send(new Packet(forwardedPacket));
        }

        public IAsyncResult Send(Packet packet)
        {
            #region LOGGING
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
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).DownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).PlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).Score} : {((packet.SpecificPacket as Event).ChangedObject as Player).StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).SelectedDifficulty})";
                }
            }
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes: ({packet.Type}) ({secondaryInfo})");
            #endregion LOGGING

            packet.From = Self?.Id ?? Guid.Empty;
            return client.Send(packet.ToBytes());
        }

        #region EVENTS/ACTIONS
        public void AddPlayer(Player player)
        {
            var @event = new Event();
            @event.Type = Event.EventType.PlayerAdded;
            @event.ChangedObject = player;
            Send(new Packet(@event));
        }

        private void AddPlayerReceived(Player player)
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

        public void UpdatePlayerReceived(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers[newPlayers.FindIndex(x => x.Id == player.Id)] = player;
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            //If the player updated is *us* (an example of this coming from the outside is stream sync info)
            //we should update our Self
            if (Self.Id == player.Id) Self = player;

            PlayerInfoUpdated?.Invoke(player);
        }

        public void RemovePlayer(Player player)
        {
            var @event = new Event();
            @event.Type = Event.EventType.PlayerLeft;
            @event.ChangedObject = player;
            Send(new Packet(@event));
        }

        private void RemovePlayerReceived(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.RemoveAll(x => x.Id == player.Id);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            PlayerDisconnected?.Invoke(player);
        }

        public void AddCoordinator(Coordinator coordinator)
        {
            var @event = new Event();
            @event.Type = Event.EventType.CoordinatorAdded;
            @event.ChangedObject = coordinator;
            Send(new Packet(@event));
        }

        private void AddCoordinatorReceived(Coordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.Add(coordinator);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void RemoveCoordinator(Coordinator coordinator)
        {
            var @event = new Event();
            @event.Type = Event.EventType.CoordinatorLeft;
            @event.ChangedObject = coordinator;
            Send(new Packet(@event));
        }

        private void RemoveCoordinatorReceived(Coordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.RemoveAll(x => x.Id == coordinator.Id);
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

        private void AddMatchReceived(Match match)
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

        public void UpdateMatchReceived(Match match)
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

        private void DeleteMatchReceived(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.RemoveAll(x => x.Guid == match.Guid);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            MatchDeleted?.Invoke(match);
        }

        private void AddQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var newEvents = State.Events.ToList();
            newEvents.Add(qualifierEvent);
            State.Events = newEvents.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void UpdateQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event();
            @event.Type = Event.EventType.QualifierEventUpdated;
            @event.ChangedObject = qualifierEvent;
            Send(new Packet(@event));
        }

        public void UpdateQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var newEvents = State.Events.ToList();
            newEvents[newEvents.FindIndex(x => x.EventId == qualifierEvent.EventId)] = qualifierEvent;
            State.Events = newEvents.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public void DeleteQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event();
            @event.Type = Event.EventType.QualifierEventDeleted;
            @event.ChangedObject = qualifierEvent;
            Send(new Packet(@event));
        }

        private void DeleteQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var newEvents = State.Events.ToList();
            newEvents.RemoveAll(x => x.EventId == qualifierEvent.EventId);
            State.Events = newEvents.ToArray();
            NotifyPropertyChanged(nameof(State));
        }
        #endregion EVENTS/ACTIONS

        protected virtual void Client_PacketReceived(Packet packet)
        {
            #region LOGGING
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
                if ((packet.SpecificPacket as Event).Type == Event.EventType.PlayerUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} from ({((packet.SpecificPacket as Event).ChangedObject as Player).Name} : {((packet.SpecificPacket as Event).ChangedObject as Player).DownloadState}) : ({((packet.SpecificPacket as Event).ChangedObject as Player).PlayState} : {((packet.SpecificPacket as Event).ChangedObject as Player).Score} : {((packet.SpecificPacket as Event).ChangedObject as Player).StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject as Match).SelectedDifficulty})";
                }
            }
            Logger.Debug($"Received {packet.ToBytes().Length} bytes: ({packet.Type}) ({secondaryInfo})");
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
                    case Event.EventType.CoordinatorAdded:
                        AddCoordinatorReceived(@event.ChangedObject as Coordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        RemoveCoordinatorReceived(@event.ChangedObject as Coordinator);
                        break;
                    case Event.EventType.MatchCreated:
                        AddMatchReceived(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchUpdated:
                        UpdateMatchReceived(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        DeleteMatchReceived(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        AddPlayerReceived(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerUpdated:
                        UpdatePlayerReceived(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        RemovePlayerReceived(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.QualifierEventCreated:
                        AddQualifierEventReceived(@event.ChangedObject as QualifierEvent);
                        break;
                    case Event.EventType.QualifierEventUpdated:
                        UpdateQualifierEventReceived(@event.ChangedObject as QualifierEvent);
                        break;
                    case Event.EventType.QualifierEventDeleted:
                        DeleteQualifierEventReceived(@event.ChangedObject as QualifierEvent);
                        break;
                    case Event.EventType.HostAdded:
                        break;
                    case Event.EventType.HostRemoved:
                        break;
                    default:
                        Logger.Error($"Unknown command received!");
                        break;
                }
            }
            else if (packet.Type == PacketType.ConnectResponse)
            {
                var response = packet.SpecificPacket as ConnectResponse;
                if (response.Type == Response.ResponseType.Success)
                {
                    Self = response.Self;
                    State = response.State;
                    ConnectedToServer?.Invoke(response);
                }
                else if (response.Type == Response.ResponseType.Fail)
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
