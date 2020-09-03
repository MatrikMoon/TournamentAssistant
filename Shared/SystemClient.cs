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

        public event Action<SongFinished> PlayerFinishedSong;

        public event Action<Acknowledgement, Guid> AckReceived;

        public event Action<ConnectResponse> ConnectedToServer;
        public event Action<ConnectResponse> FailedToConnectToServer;
        public event Action ServerDisconnected;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //Tournament State in the client *should* only be modified by the server connection thread, so thread-safety shouldn't be an issue here
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
        private bool shouldHeartbeat;
        private string endpoint;
        private int port;
        private string username;
        private string userId;
        private ConnectTypes connectType;

        public SystemClient(string endpoint, int port, string username, ConnectTypes connectType, string userId = "0")
        {
            this.endpoint = endpoint;
            this.port = port;
            this.username = username;
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
            if (shouldHeartbeat) heartbeatTimer.Start();

            Send(new Packet(new Connect()
            {
                ClientType = connectType,
                Name = username,
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

        protected virtual void Client_PacketRecieved(Packet packet)
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
            Logger.Debug($"Recieved {packet.ToBytes().Length} bytes: ({packet.Type}) ({secondaryInfo})");
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
                        AddCoordinatorRecieved(@event.ChangedObject as Coordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        RemoveCoordinatorRecieved(@event.ChangedObject as Coordinator);
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
                if (response.Type == ConnectResponse.ResponseType.Success)
                {
                    Self = response.Self;
                    State = response.State;
                    ConnectedToServer?.Invoke(response);
                }
                else if (response.Type == ConnectResponse.ResponseType.Fail)
                {
                    FailedToConnectToServer?.Invoke(response);
                }
            }
            else if (packet.Type == PacketType.SongFinished)
            {
                PlayerFinishedSong?.Invoke(packet.SpecificPacket as SongFinished);
            }
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

        public void Send(Packet packet)
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
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes: ({packet.Type}) ({secondaryInfo})");
            #endregion LOGGING

            packet.From = Self?.Id ?? Guid.Empty;
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
            newPlayers[newPlayers.FindIndex(x => x.Id == player.Id)] = player;
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            //IN-TESTING:
            //If the player updated is *us* (an example of this coming from the outside is stream sync info)
            //we should update our Self
            if (Self == player) Self = player;

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

        private void AddCoordinatorRecieved(Coordinator coordinator)
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

        private void RemoveCoordinatorRecieved(Coordinator coordinator)
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
