using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Models.Packets.Connect;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantShared
{
    public class SystemClient : INotifyPropertyChanged
    {
        public event Func<Player, Task> PlayerConnected;
        public event Func<Player, Task> PlayerDisconnected;
        public event Func<Player, Task> PlayerInfoUpdated;
        public event Func<Match, Task> MatchInfoUpdated;
        public event Func<Match, Task> MatchCreated;
        public event Func<Match, Task> MatchDeleted;

        public event Func<Acknowledgement, Guid, Task> AckReceived;
        public event Func<SongFinished, Task> PlayerFinishedSong;

        public event Func<ConnectResponse, Task> ConnectedToServer;
        public event Func<ConnectResponse, Task> FailedToConnectToServer;
        public event Func<Task> ServerDisconnected;

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

        private Timer heartbeatTimer = new();
        private bool shouldHeartbeat;
        private string endpoint;
        private int port;
        private string username;
        private string password;
        private string userId;
        private ConnectTypes connectType;

        public SystemClient(string endpoint, int port, string username, ConnectTypes connectType, string userId = "0", string password = null)
        {
            this.endpoint = endpoint;
            this.port = port;
            this.username = username;
            this.password = password;
            this.userId = userId;
            this.connectType = connectType;
        }

        //Blocks until connected (or failed), then returns
        public async Task Start()
        {
            shouldHeartbeat = true;
            heartbeatTimer.Interval = 10000;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;

            await ConnectToServer();
        }

        private void HeartbeatTimer_Elapsed(object _, ElapsedEventArgs __)
        {
            //Send needs to be awaited so it will properly catch exceptions, but we can't make this timer callback async. So, we do this.
            async Task timerAction()
            {
                try
                {
                    var command = new Command
                    {
                        CommandType = Command.CommandTypes.Heartbeat
                    };
                    await Send(new Packet(command));
                }
                catch (Exception e)
                {
                    Logger.Debug("HEARTBEAT FAILED");
                    Logger.Debug(e.ToString());

                    await ConnectToServer();
                }
            }
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            timerAction();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private async Task ConnectToServer()
        {
            //Don't heartbeat while connecting
            heartbeatTimer.Stop();

            try
            {
                State = new State
                {
                    Players = new Player[0],
                    Coordinators = new Coordinator[0],
                    Matches = new Match[0]
                };

                client = new Sockets.Client(endpoint, port);
                client.PacketReceived += Client_PacketReceived;
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
            if (shouldHeartbeat) heartbeatTimer.Start();

            await Send(new Packet(new Connect()
            {
                ClientType = connectType,
                Name = username,
                Password = password,
                UserId = userId,
                ClientVersion = SharedConstructs.VersionCode
            }));
        }

        private async Task Client_ServerFailedToConnect()
        {
            //Resume heartbeat if we fail to connect
            //Basically the same as just doing another connect here...
            //But with some extra delay. I don't really know why
            //I'm doing it this way
            if (shouldHeartbeat) heartbeatTimer.Start();

            if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(null);
        }

        private async Task Client_ServerDisconnected()
        {
            Logger.Debug("Server disconnected!");
            if (ServerDisconnected != null) await ServerDisconnected.Invoke();
        }

        public void Shutdown()
        {
            client?.Shutdown();
            heartbeatTimer.Stop();

            //If the client was connecting when we shut it down, the FailedToConnect event might resurrect the heartbeat without this
            shouldHeartbeat = false;
        }

        public Task Send(Guid id, Packet packet) => Send(new Guid[] { id }, packet);

        public Task Send(Guid[] ids, Packet packet)
        {
            packet.From = Self?.Id ?? Guid.Empty;

            var forwardedPacket = new ForwardingPacket
            {
                ForwardTo = ids,
                Type = packet.Type,
                SpecificPacket = packet.SpecificPacket
            };

            return Send(new Packet(forwardedPacket));
        }

        public Task Send(Packet packet)
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
        public async Task AddPlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.EventType.PlayerAdded,
                ChangedObject = player
            };
            await Send(new Packet(@event));
        }

        private async Task AddPlayerReceived(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.Add(player);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            if (PlayerConnected != null) await PlayerConnected.Invoke(player);
        }

        public async Task UpdatePlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.EventType.PlayerUpdated,
                ChangedObject = player
            };
            await Send(new Packet(@event));
        }

        public async Task UpdatePlayerReceived(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers[newPlayers.FindIndex(x => x.Id == player.Id)] = player;
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            //If the player updated is *us* (an example of this coming from the outside is stream sync info)
            //we should update our Self
            if (Self.Id == player.Id) Self = player;

            if (PlayerInfoUpdated != null) await PlayerInfoUpdated.Invoke(player);
        }

        public async Task RemovePlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.EventType.PlayerLeft,
                ChangedObject = player
            };
            await Send(new Packet(@event));
        }

        private async Task RemovePlayerReceived(Player player)
        {
            var newPlayers = State.Players.ToList();
            newPlayers.RemoveAll(x => x.Id == player.Id);
            State.Players = newPlayers.ToArray();
            NotifyPropertyChanged(nameof(State));

            if (PlayerDisconnected != null) await PlayerDisconnected.Invoke(player);
        }

        public async Task AddCoordinator(Coordinator coordinator)
        {
            var @event = new Event
            {
                Type = Event.EventType.CoordinatorAdded,
                ChangedObject = coordinator
            };
            await Send(new Packet(@event));
        }

        private void AddCoordinatorReceived(Coordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.Add(coordinator);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public async Task RemoveCoordinator(Coordinator coordinator)
        {
            var @event = new Event
            {
                Type = Event.EventType.CoordinatorLeft,
                ChangedObject = coordinator
            };
            await Send(new Packet(@event));
        }

        private void RemoveCoordinatorReceived(Coordinator coordinator)
        {
            var newCoordinators = State.Coordinators.ToList();
            newCoordinators.RemoveAll(x => x.Id == coordinator.Id);
            State.Coordinators = newCoordinators.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public async Task CreateMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.EventType.MatchCreated,
                ChangedObject = match
            };
            await Send(new Packet(@event));
        }

        private async Task AddMatchReceived(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.Add(match);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            if (MatchCreated != null)
                await MatchCreated.Invoke(match);
        }

        public async Task UpdateMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.EventType.MatchUpdated,
                ChangedObject = match
            };
            await Send(new Packet(@event));
        }

        public async Task UpdateMatchReceived(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches[newMatches.FindIndex(x => x.Guid == match.Guid)] = match;
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            if (MatchInfoUpdated != null) await MatchInfoUpdated.Invoke(match);
        }

        public async Task DeleteMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.EventType.MatchDeleted,
                ChangedObject = match
            };
            await Send(new Packet(@event));
        }

        private async Task DeleteMatchReceived(Match match)
        {
            var newMatches = State.Matches.ToList();
            newMatches.RemoveAll(x => x.Guid == match.Guid);
            State.Matches = newMatches.ToArray();
            NotifyPropertyChanged(nameof(State));

            if (MatchDeleted != null) await MatchDeleted?.Invoke(match);
        }

        private void AddQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var newEvents = State.Events.ToList();
            newEvents.Add(qualifierEvent);
            State.Events = newEvents.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public async Task UpdateQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                Type = Event.EventType.QualifierEventUpdated,
                ChangedObject = qualifierEvent
            };
            await Send(new Packet(@event));
        }

        public void UpdateQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var newEvents = State.Events.ToList();
            newEvents[newEvents.FindIndex(x => x.EventId == qualifierEvent.EventId)] = qualifierEvent;
            State.Events = newEvents.ToArray();
            NotifyPropertyChanged(nameof(State));
        }

        public async Task DeleteQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                Type = Event.EventType.QualifierEventDeleted,
                ChangedObject = qualifierEvent
            };
            await Send(new Packet(@event));
        }

        private void DeleteQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var newEvents = State.Events.ToList();
            newEvents.RemoveAll(x => x.EventId == qualifierEvent.EventId);
            State.Events = newEvents.ToArray();
            NotifyPropertyChanged(nameof(State));
        }
        #endregion EVENTS/ACTIONS

        protected virtual async Task Client_PacketReceived(Packet packet)
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
                if (AckReceived != null) await AckReceived.Invoke(acknowledgement, packet.From);
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
                        await AddMatchReceived(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchUpdated:
                        await UpdateMatchReceived(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        await DeleteMatchReceived (@event.ChangedObject as Match);
                        break;
                    case Event.EventType.PlayerAdded:
                        await AddPlayerReceived(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerUpdated:
                        await UpdatePlayerReceived(@event.ChangedObject as Player);
                        break;
                    case Event.EventType.PlayerLeft:
                        await RemovePlayerReceived(@event.ChangedObject as Player);
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
                    if (ConnectedToServer != null) await ConnectedToServer.Invoke(response);
                }
                else if (response.Type == Response.ResponseType.Fail)
                {
                    if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(response);
                }
            }
            else if (packet.Type == PacketType.SongFinished)
            {
                if (PlayerFinishedSong != null) await PlayerFinishedSong.Invoke(packet.SpecificPacket as SongFinished);
            }
        }
    }
}
