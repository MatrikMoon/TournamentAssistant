using Google.Protobuf.WellKnownTypes;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Utillities;
using static TournamentAssistantShared.Models.Packets.Connect.Types;

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
                        CommandType = Command.Types.CommandTypes.Heartbeat
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
                State = new State();

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
                Password = password ?? "",
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
            packet.From = Guid.Parse(Self?.Id ?? Guid.Empty.ToString());

            var forwardedPacket = new ForwardingPacket
            {
                ForwardTo = { ids.Select(x => x.ToString()).ToArray() },
                SpecificPacket = Any.Pack(packet.SpecificPacket)
            };

            return Send(new Packet(forwardedPacket));
        }

        public Task Send(Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.PlaySong")
            {
                var playSong = packet.SpecificPacket.Unpack<PlaySong>();
                secondaryInfo = playSong.GameplayParameters.Beatmap.LevelId + " : " + playSong.GameplayParameters.Beatmap.Difficulty;
            }
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.LoadSong")
            {
                var loadSong = packet.SpecificPacket.Unpack<LoadSong>();
                secondaryInfo = loadSong.LevelId;
            }
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.Command")
            {
                var command = packet.SpecificPacket.Unpack<Command>();
                secondaryInfo = command.CommandType.ToString();
            }
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.Event")
            {
                var @event = packet.SpecificPacket.Unpack<Event>();

                secondaryInfo = @event.Type.ToString();
                if (@event.Type == Event.Types.EventType.PlayerUpdated)
                {
                    var player = @event.ChangedObject.Unpack<Player>();
                    secondaryInfo = $"{secondaryInfo} from ({player.User.Name} : {player.DownloadState}) : ({player.PlayState} : {player.Score} : {player.StreamDelayMs})";
                }
                else if (@event.Type == Event.Types.EventType.MatchUpdated)
                {
                    var match = @event.ChangedObject.Unpack<Match>();
                    secondaryInfo = $"{secondaryInfo} ({match.SelectedDifficulty})";
                }
            }
            Logger.Debug($"Sending {packet.ToBytes().Length} bytes: ({packet.SpecificPacket.TypeUrl.Substring(packet.SpecificPacket.TypeUrl.LastIndexOf("."))}) ({secondaryInfo})");
            #endregion LOGGING

            packet.From = Guid.Parse(Self?.Id ?? Guid.Empty.ToString());
            return client.Send(packet.ToBytes());
        }

        #region EVENTS/ACTIONS
        public async Task AddPlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerAdded,
                ChangedObject = Any.Pack(player)
            };
            await Send(new Packet(@event));
        }

        private async Task AddPlayerReceived(Player player)
        {
            State.Players.Add(player);
            NotifyPropertyChanged(nameof(State));

            if (PlayerConnected != null) await PlayerConnected.Invoke(player);
        }

        public async Task UpdatePlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerUpdated,
                ChangedObject = Any.Pack(player)
            };
            await Send(new Packet(@event));
        }

        public async Task UpdatePlayerReceived(Player player)
        {
            var playerToReplace = State.Players.FirstOrDefault(x => x.User.UserEquals(player.User));
            State.Players.Remove(playerToReplace);
            State.Players.Add(player);
            NotifyPropertyChanged(nameof(State));

            //If the player updated is *us* (an example of this coming from the outside is stream sync info)
            //we should update our Self
            if (Self.Id == player.User.Id) Self = player.User;

            if (PlayerInfoUpdated != null) await PlayerInfoUpdated.Invoke(player);
        }

        public async Task RemovePlayer(Player player)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerLeft,
                ChangedObject = Any.Pack(player)
            };
            await Send(new Packet(@event));
        }

        private async Task RemovePlayerReceived(Player player)
        {
            var playerToRemove = State.Players.FirstOrDefault(x => x.User.UserEquals(player.User));
            State.Players.Remove(playerToRemove);
            NotifyPropertyChanged(nameof(State));

            if (PlayerDisconnected != null) await PlayerDisconnected.Invoke(player);
        }

        public async Task AddCoordinator(Coordinator coordinator)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.CoordinatorAdded,
                ChangedObject = Any.Pack(coordinator)
            };
            await Send(new Packet(@event));
        }

        private void AddCoordinatorReceived(Coordinator coordinator)
        {
            State.Coordinators.Add(coordinator);
            NotifyPropertyChanged(nameof(State));
        }

        public async Task RemoveCoordinator(Coordinator coordinator)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.CoordinatorLeft,
                ChangedObject = Any.Pack(coordinator)
            };
            await Send(new Packet(@event));
        }

        private void RemoveCoordinatorReceived(Coordinator coordinator)
        {
            var coordinatorToRemove = State.Coordinators.FirstOrDefault(x => x.User.UserEquals(coordinator.User));
            State.Coordinators.Remove(coordinatorToRemove);
            NotifyPropertyChanged(nameof(State));
        }

        public async Task CreateMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.MatchCreated,
                ChangedObject = Any.Pack(match)
            };
            await Send(new Packet(@event));
        }

        private async Task AddMatchReceived(Match match)
        {
            State.Matches.Add(match);
            NotifyPropertyChanged(nameof(State));

            if (MatchCreated != null) await MatchCreated.Invoke(match);
        }

        public async Task UpdateMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.MatchUpdated,
                ChangedObject = Any.Pack(match)
            };
            await Send(new Packet(@event));
        }

        public async Task UpdateMatchReceived(Match match)
        {
            var matchToReplace = State.Matches.FirstOrDefault(x => x.Guid == match.Guid);
            State.Matches.Remove(matchToReplace);
            State.Matches.Add(match);
            NotifyPropertyChanged(nameof(State));

            if (MatchInfoUpdated != null) await MatchInfoUpdated.Invoke(match);
        }

        public async Task DeleteMatch(Match match)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.MatchDeleted,
                ChangedObject = Any.Pack(match)
            };
            await Send(new Packet(@event));
        }

        private async Task DeleteMatchReceived(Match match)
        {
            var matchToRemove = State.Matches.FirstOrDefault(x => x.Guid == match.Guid);
            State.Matches.Remove(matchToRemove);
            NotifyPropertyChanged(nameof(State));

            if (MatchDeleted != null) await MatchDeleted?.Invoke(match);
        }

        private void AddQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            State.Events.Add(qualifierEvent);
            NotifyPropertyChanged(nameof(State));
        }

        public async Task UpdateQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.QualifierEventUpdated,
                ChangedObject = Any.Pack(qualifierEvent)
            };
            await Send(new Packet(@event));
        }

        public void UpdateQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var eventToReplace = State.Events.FirstOrDefault(x => x.EventId == qualifierEvent.EventId);
            State.Events.Remove(eventToReplace);
            State.Events.Add(qualifierEvent);
            NotifyPropertyChanged(nameof(State));
        }

        public async Task DeleteQualifierEvent(QualifierEvent qualifierEvent)
        {
            var @event = new Event
            {
                Type = Event.Types.EventType.QualifierEventDeleted,
                ChangedObject = Any.Pack(qualifierEvent)
            };
            await Send(new Packet(@event));
        }

        private void DeleteQualifierEventReceived(QualifierEvent qualifierEvent)
        {
            var eventToRemove = State.Events.FirstOrDefault(x => x.EventId == qualifierEvent.EventId);
            State.Events.Remove(eventToRemove);
            NotifyPropertyChanged(nameof(State));
        }
        #endregion EVENTS/ACTIONS

        protected virtual async Task Client_PacketReceived(Packet packet)
        {
            #region LOGGING
            string secondaryInfo = string.Empty;
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.PlaySong")
            {
                var playSong = packet.SpecificPacket.Unpack<PlaySong>();
                secondaryInfo = playSong.GameplayParameters.Beatmap.LevelId + " : " + playSong.GameplayParameters.Beatmap.Difficulty;
            }
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.LoadSong")
            {
                var loadSong = packet.SpecificPacket.Unpack<LoadSong>();
                secondaryInfo = loadSong.LevelId;
            }
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.Command")
            {
                var command = packet.SpecificPacket.Unpack<Command>();
                secondaryInfo = command.CommandType.ToString();
            }
            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.Event")
            {
                var @event = packet.SpecificPacket.Unpack<Event>();

                secondaryInfo = @event.Type.ToString();
                if (@event.Type == Event.Types.EventType.PlayerUpdated)
                {
                    var player = @event.ChangedObject.Unpack<Player>();
                    secondaryInfo = $"{secondaryInfo} from ({player.User.Name} : {player.DownloadState}) : ({player.PlayState} : {player.Score} : {player.StreamDelayMs})";
                }
                else if (@event.Type == Event.Types.EventType.MatchUpdated)
                {
                    var match = @event.ChangedObject.Unpack<Match>();
                    secondaryInfo = $"{secondaryInfo} ({match.SelectedDifficulty})";
                }
            }
            Logger.Debug($"Received {packet.ToBytes().Length} bytes: ({packet.SpecificPacket.TypeUrl.Substring(packet.SpecificPacket.TypeUrl.LastIndexOf("."))}) ({secondaryInfo})");
            #endregion LOGGING

            //Ready to go, only disabled since it is currently unusued
            /*if (packet.Type != PacketType.Acknowledgement)
            {
                Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id
                }));
            }*/

            if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.Acknowledgement")
            {
                var acknowledgement = packet.SpecificPacket.Unpack<Acknowledgement>();
                if (AckReceived != null) await AckReceived.Invoke(acknowledgement, packet.From);
            }
            else if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.Event")
            {
                var @event = packet.SpecificPacket.Unpack<Event>();
                switch (@event.Type)
                {
                    case Event.Types.EventType.CoordinatorAdded:
                        AddCoordinatorReceived(@event.ChangedObject.Unpack<Coordinator>());
                        break;
                    case Event.Types.EventType.CoordinatorLeft:
                        RemoveCoordinatorReceived(@event.ChangedObject.Unpack<Coordinator>());
                        break;
                    case Event.Types.EventType.MatchCreated:
                        await AddMatchReceived(@event.ChangedObject.Unpack<Match>());
                        break;
                    case Event.Types.EventType.MatchUpdated:
                        await UpdateMatchReceived(@event.ChangedObject.Unpack<Match>());
                        break;
                    case Event.Types.EventType.MatchDeleted:
                        await DeleteMatchReceived (@event.ChangedObject.Unpack<Match>());
                        break;
                    case Event.Types.EventType.PlayerAdded:
                        await AddPlayerReceived(@event.ChangedObject.Unpack<Player>());
                        break;
                    case Event.Types.EventType.PlayerUpdated:
                        await UpdatePlayerReceived(@event.ChangedObject.Unpack<Player>());
                        break;
                    case Event.Types.EventType.PlayerLeft:
                        await RemovePlayerReceived(@event.ChangedObject.Unpack<Player>());
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
            else if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.ConnectResponse")
            {
                var response = packet.SpecificPacket.Unpack<ConnectResponse>();
                if (response.Response.Type == Response.Types.ResponseType.Success)
                {
                    Self = response.Self;
                    State = response.State;
                    if (ConnectedToServer != null) await ConnectedToServer.Invoke(response);
                }
                else if (response.Response.Type == Response.Types.ResponseType.Fail)
                {
                    if (FailedToConnectToServer != null) await FailedToConnectToServer.Invoke(response);
                }
            }
            else if (packet.SpecificPacket.TypeUrl == "type.googleapis.com/TournamentAssistantShared.Models.Packets.SongFinished")
            {
                if (PlayerFinishedSong != null) await PlayerFinishedSong.Invoke(packet.SpecificPacket.Unpack<SongFinished>());
            }
        }
    }
}
