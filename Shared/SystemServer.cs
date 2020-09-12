using Open.Nat;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.Discord;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using TournamentAssistantShared.Discord.Helpers;
using static TournamentAssistantShared.Packet;
using TournamentAssistantShared.SimpleJSON;

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

        public QualifierBot QualifierBot { get; private set; }

        //Server settings
        private int port;
        private ServerSettings settings;
        private string botToken;

        //Overlay settings
        private int overlayPort;

        public SystemServer(string botTokenArg = null)
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
                scoreUpdateFrequencyValue = "30";
                config.SaveString("scoreUpdateFrequency", scoreUpdateFrequencyValue);
            }

            var overlayPortValue = config.GetString("overlayPort");
            if (overlayPortValue == string.Empty || overlayPortValue == "[overlayPort]")
            {
                overlayPortValue = "0";
                config.SaveString("overlayPort", "[overlayPort]");
            }

            var botTokenValue = config.GetString("botToken");
            if (botTokenValue == string.Empty || botTokenValue == "[botToken]")
            {
                botTokenValue = botTokenArg;
                config.SaveString("botToken", "[botToken]");
            }

            var bannedModsValue = config.GetBannedMods();
            if (bannedModsValue.Length == 0)
            {
                bannedModsValue = new string[] { "IntroSkip", "AutoPauseStealth", "NoteSliceVisualizer", "SongChartVisualizer", "Custom Notes" };
                config.SaveBannedMods(bannedModsValue);
            }

            var enableTeamsValue = config.GetBoolean("enableTeams");

            var teamsValue = config.GetTeams();
            if (teamsValue.Length == 0)
            {
                //Default teams
                teamsValue = new Team[]
                {
                    new Team()
                    {
                        Id = Guid.NewGuid(),
                        Name = "Team Green"
                    },
                    new Team()
                    {
                        Id = Guid.NewGuid(),
                        Name = "Team Spicy"
                    },
                };
                config.SaveTeams(teamsValue);
            }

            settings = new ServerSettings();
            settings.ServerName = nameValue;
            settings.EnableTeams = enableTeamsValue;
            settings.Teams = teamsValue;
            settings.ScoreUpdateFrequency = Convert.ToInt32(scoreUpdateFrequencyValue);
            settings.BannedMods = bannedModsValue;

            port = int.Parse(portValue);
            overlayPort = int.Parse(overlayPortValue);
            botToken = botTokenValue;
        }

        public async void Start()
        {
            State = new State();
            State.ServerSettings = settings;
            State.Players = new Player[0];
            State.Coordinators = new Coordinator[0];
            State.Matches = new Match[0];

            if (overlayPort != 0)
            {
                OpenPort(overlayPort);
                overlayServer = new Server(overlayPort);

                #pragma warning disable CS4014
                Task.Run(overlayServer.Start);
                #pragma warning restore CS4014
            }

            //If we have a token, start a qualifier bot
            if (!string.IsNullOrEmpty(botToken) && botToken != "[botToken]")
            {
                //We need to await this so the DI framework has time to load the database service
                QualifierBot = new QualifierBot(botToken: botToken);
                await QualifierBot.Start();
            }

            if (QualifierBot != null)
            {
                //Translate Event and Songs from database to model format
                var events = QualifierBot.Database.Events.Where(x => !x.Old);
                State.Events = events.Select(x => QualifierBot.Database.ConvertDatabaseToModel(x)).ToArray();

                //No event removals because we don't expect this to ever shut down
                QualifierBot.Database.QualifierEventCreated += (@event) => CreateQualifierEvent(@event);
                QualifierBot.Database.QualifierEventUpdated += (@event) => UpdateQualifierEvent(@event);
                QualifierBot.Database.QualifierEventDeleted += (@event) => DeleteQualifierEvent(@event);
            }

            Self = new Coordinator()
            {
                Id = Guid.Empty,
                Name = "HOST"
            };

            OpenPort(port);

            server = new Server(port);
            server.PacketReceived += Server_PacketReceived;
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;

            #pragma warning disable CS4014
            Task.Run(() => server.Start());
            #pragma warning restore CS4014
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
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }

            var toIds = string.Empty;
            foreach (var id in ids) toIds += $"{id}, ";
            if (!string.IsNullOrEmpty(toIds)) toIds = toIds.Substring(0, toIds.Length - 2);

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

        public void AddCoordinator(Coordinator coordinator)
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

        public void RemoveCoordinator(Coordinator coordinator)
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

        public void CreateQualifierEvent(QualifierEvent qualifierEvent)
        {
            lock (State)
            {
                var newEvents = State.Events.ToList();
                newEvents.Add(qualifierEvent);
                State.Events = newEvents.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.Type = Event.EventType.QualifierEventCreated;
            @event.ChangedObject = qualifierEvent;
            BroadcastToAllClients(new Packet(@event));
        }

        public void UpdateQualifierEvent(QualifierEvent qualifierEvent)
        {
            lock (State)
            {
                var newEvents = State.Events.ToList();
                newEvents[newEvents.FindIndex(x => x.EventId == qualifierEvent.EventId)] = qualifierEvent;
                State.Events = newEvents.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.Type = Event.EventType.QualifierEventUpdated;
            @event.ChangedObject = qualifierEvent;

            var updatePacket = new Packet(@event);

            BroadcastToAllClients(updatePacket);
        }

        public void DeleteQualifierEvent(QualifierEvent qualifierEvent)
        {
            lock (State)
            {
                var newEvents = State.Events.ToList();
                newEvents.RemoveAll(x => x.EventId == qualifierEvent.EventId);
                State.Events = newEvents.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.Type = Event.EventType.QualifierEventDeleted;
            @event.ChangedObject = qualifierEvent;
            BroadcastToAllClients(new Packet(@event));
        }
        #endregion EventManagement

        private void Server_PacketReceived(ConnectedClient player, Packet packet)
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
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }
            Logger.Debug($"Received {packet.ToBytes().Length} bytes: ({packet.Type}) ({secondaryInfo})");
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
            /*else if (packet.Type == PacketType.SongList)
            {
                SongList songList = packet.SpecificPacket as SongList;
            }*/
            /*else if (packet.Type == PacketType.LoadedSong)
            {
                LoadedSong loadedSong = packet.SpecificPacket as LoadedSong;
            }*/
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
                        Message = $"Connected to {settings.ServerName}!",
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
                else if (connect.ClientType == Connect.ConnectTypes.Coordinator)
                {
                    var coordinator = new Coordinator()
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
                        Message = $"Connected to {settings.ServerName}!",
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
                else if (connect.ClientType == Connect.ConnectTypes.TemporaryConnection)
                {
                    //A scraper just wants a copy of our state, so let's give it to them
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Type = ConnectResponse.ResponseType.Success,
                        Self = null,
                        State = State,
                        Message = $"Connected to {settings.ServerName} (scraper)!",
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
            }
            else if (packet.Type == PacketType.ScoreRequest)
            {
                ScoreRequest request = packet.SpecificPacket as ScoreRequest;

                var scores = QualifierBot.Database.Scores
                    .Where(x => x.EventId == request.EventId.ToString() &&
                        x.LevelId == request.Parameters.Beatmap.LevelId &&
                        x.Characteristic == request.Parameters.Beatmap.Characteristic.SerializedName &&
                        x.BeatmapDifficulty == (int)request.Parameters.Beatmap.Difficulty &&
                        x.GameOptions == (int)request.Parameters.GameplayModifiers.Options &&
                        //x.PlayerOptions == (int)request.Parameters.PlayerSettings.Options &&
                        !x.Old).OrderBy(x => x._Score).Take(10)
                    .Select(x => new Score
                {
                    EventId = request.EventId,
                    Parameters = request.Parameters,
                    Username = x.Username,
                    UserId = x.UserId,
                    _Score = x._Score,
                    FullCombo = x.FullCombo,
                    Color = "#ffffff"
                });

                Send(player.id, new Packet(new ScoreRequestResponse
                {
                    Scores = scores.ToArray()
                }));
            }
            else if (packet.Type == PacketType.SubmitScore)
            {
                SubmitScore submitScore = packet.SpecificPacket as SubmitScore;

                //Check to see if the song exists in the database
                var song = QualifierBot.Database.Songs.FirstOrDefault(x => x.EventId == submitScore.Score.EventId.ToString() &&
                        x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                        x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                        x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                        x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                        //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                        !x.Old);

                if (song != null)
                {
                    //Mark all older scores as old
                    var scores = QualifierBot.Database.Scores
                        .Where(x => x.EventId == submitScore.Score.EventId.ToString() &&
                            x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                            x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                            x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                            x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                            //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                            !x.Old);

                    var oldHighScore = (scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? 0);

                    if ((scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? 0) < submitScore.Score._Score)
                    {
                        scores.ForEach(x => x.Old = true);

                        QualifierBot.Database.Scores.Add(new Discord.Database.Score
                        {
                            EventId = submitScore.Score.EventId.ToString(),
                            UserId = submitScore.Score.UserId,
                            Username = submitScore.Score.Username,
                            LevelId = submitScore.Score.Parameters.Beatmap.LevelId,
                            Characteristic = submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName,
                            BeatmapDifficulty = (int)submitScore.Score.Parameters.Beatmap.Difficulty,
                            GameOptions = (int)submitScore.Score.Parameters.GameplayModifiers.Options,
                            PlayerOptions = (int)submitScore.Score.Parameters.PlayerSettings.Options,
                            _Score = submitScore.Score._Score,
                            FullCombo = submitScore.Score.FullCombo,
                        });
                        QualifierBot.Database.SaveChanges();
                    }

                    var newScores = QualifierBot.Database.Scores
                        .Where(x => x.EventId == submitScore.Score.EventId.ToString() &&
                            x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                            x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                            x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                            x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                            //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                            !x.Old).OrderBy(x => x._Score).Take(10)
                        .Select(x => new Score
                        {
                            EventId = submitScore.Score.EventId,
                            Parameters = submitScore.Score.Parameters,
                            Username = x.Username,
                            UserId = x.UserId,
                            _Score = x._Score,
                            FullCombo = x.FullCombo,
                            Color = "#ffffff"
                        });

                    //IN-TESTING: Return the new scores for the song so the leaderboard will update immediately
                    Send(player.id, new Packet(new ScoreRequestResponse
                    {
                        Scores = newScores.ToArray()
                    }));
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.Type)
                {
                    case Event.EventType.CoordinatorAdded:
                        AddCoordinator(@event.ChangedObject as Coordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        RemoveCoordinator(@event.ChangedObject as Coordinator);
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
                    case Event.EventType.QualifierEventCreated:
                        CreateQualifierEvent(@event.ChangedObject as QualifierEvent);
                        break;
                    case Event.EventType.QualifierEventUpdated:
                        UpdateQualifierEvent(@event.ChangedObject as QualifierEvent);
                        break;
                    case Event.EventType.QualifierEventDeleted:
                        DeleteQualifierEvent(@event.ChangedObject as QualifierEvent);
                        break;
                    case Event.EventType.HostAdded:
                        break;
                    case Event.EventType.HostRemoved:
                        break;
                    default:
                        Logger.Error($"Unknown command received from {player.id}!");
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

                //TODO: REMOVE
                /*var scoreboardClient = State.Coordinators.FirstOrDefault(x => x.Name == "[Scoreboard]");
                if (scoreboardClient != null) forwardingPacket.ForwardTo = forwardingPacket.ForwardTo.ToList().Union(new Guid[] { scoreboardClient.Id }).ToArray();*/

                ForwardTo(forwardingPacket.ForwardTo, packet.From, forwardedPacket);
            }
        }
    }
}
