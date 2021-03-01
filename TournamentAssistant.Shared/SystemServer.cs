using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Open.Nat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.Discord;
using TournamentAssistantShared.Discord.Helpers;
using TournamentAssistantShared.Discord.Services;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.SimpleJSON;
using TournamentAssistantShared.Sockets;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.GameplayModifiers.Types;
using static TournamentAssistantShared.Models.Packets.Connect.Types;
using static TournamentAssistantShared.Models.Packets.Response;
using static TournamentAssistantShared.Models.Packets.Response.Types;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using static TournamentAssistantShared.Models.PlayerSpecificSettings.Types;

namespace TournamentAssistantShared
{
    public class SystemServer : IConnection, INotifyPropertyChanged
    {
        private Server server;
        private WsServer overlayServer;

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

        public User Self { get; private set; }
        public object SelfObject { get; private set; }

        public QualifierBot QualifierBot { get; private set; }
        public Discord.Database.QualifierDatabaseContext Database { get; private set; }

        //Reference to self as a server, if we are eligible for the Master Lists
        public CoreServer CoreServer { get; private set; }

        //Server settings
        private Config config;

        private string address;
        private int port;
        private ServerSettings settings;
        private string botToken;

        //Update checker
        private CancellationTokenSource updateCheckToken = new CancellationTokenSource();

        //Overlay settings
        private int overlayPort;

        public SystemServer(string botTokenArg = null)
        {
            config = new Config("serverConfig.json");

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

            var passwordValue = config.GetString("password");
            if (passwordValue == string.Empty || passwordValue == "[Password]")
            {
                passwordValue = string.Empty;
                config.SaveString("password", "[Password]");
            }

            var addressValue = config.GetString("serverAddress");
            if (addressValue == string.Empty || addressValue == "[serverAddress]")
            {
                addressValue = "[serverAddress]";
                config.SaveString("serverAddress", addressValue);
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
                        Id = Guid.NewGuid().ToString(),
                        Name = "Team Green"
                    },
                    new Team()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Team Spicy"
                    }
                };
                config.SaveTeams(teamsValue);
            }

            settings = new ServerSettings
            {
                ServerName = nameValue,
                Password = passwordValue,
                EnableTeams = enableTeamsValue,
                ScoreUpdateFrequency = Convert.ToInt32(scoreUpdateFrequencyValue)
            };
            settings.Teams.AddRange(teamsValue);
            settings.BannedMods.AddRange(bannedModsValue);

            address = addressValue;
            port = int.Parse(portValue);
            overlayPort = int.Parse(overlayPortValue);
            botToken = botTokenValue;
        }

        public async void Start()
        {
            State = new State
            {
                ServerSettings = settings
            };
            State.KnownHosts.AddRange(config.GetHosts());

            //Check for updates
            Logger.Info("Checking for updates...");
            var newVersion = await Update.GetLatestRelease();
            if (System.Version.Parse(SharedConstructs.Version) < newVersion)
            {
                Logger.Error($"Update required! You are on \'{SharedConstructs.Version}\', new version is \'{newVersion}\'");
                return;
            }
            else Logger.Success($"You are on the most recent version! ({SharedConstructs.Version})");

            if (overlayPort != 0)
            {
                OpenPort(overlayPort);
                overlayServer = new WsServer(overlayPort);
#pragma warning disable CS4014
                Task.Run(overlayServer.Start);
#pragma warning restore CS4014
                overlayServer.PacketReceived += overlay_PacketReceived;
            }

            //If we have a token, start a qualifier bot
            if (!string.IsNullOrEmpty(botToken) && botToken != "[botToken]")
            {
                //We need to await this so the DI framework has time to load the database service
                QualifierBot = new QualifierBot(botToken: botToken, server: this);
                await QualifierBot.Start();
            }

            //Set up the database
            if (QualifierBot != null)
            {
                Database = QualifierBot.Database;
            }
            else
            {
                //If the bot's not running, we need to start the service manually
                var service = new DatabaseService();
                Database = service.DatabaseContext;
            }

            //Translate Event and Songs from database to model format
            var events = Database.Events.Where(x => !x.Old);
            Func<string, List<GameplayParameters>> getSongsForEvent = (string eventId) =>
            {
                return Database.Songs.Where(x => !x.Old && x.EventId == eventId).Select(x => new GameplayParameters
                {
                    Beatmap = new Beatmap
                    {
                        LevelId = x.LevelId,
                        Characteristic = new Characteristic
                        {
                            SerializedName = x.Characteristic
                        },
                        Difficulty = (BeatmapDifficulty)x.BeatmapDifficulty,
                        Name = x.Name
                    },
                    GameplayModifiers = new GameplayModifiers
                    {
                        Options = (GameOptions)x.GameOptions
                    },
                    PlayerSettings = new PlayerSpecificSettings
                    {
                        Options = (PlayerOptions)x.PlayerOptions
                    }
                }).ToList() ?? new List<GameplayParameters> { };
            };
            State.Events.AddRange(events.Select(x => Database.ConvertDatabaseToModel(getSongsForEvent(x.EventId).ToArray(), x)));

            //Give our new server a sense of self :P
            Self = new User
            {
                Id = Guid.Empty.ToString(),
                Name = "HOST"
            };
            SelfObject = new Coordinator
            {
                Id = Self.Id,
                Name = Self.Name
            };

            Func<CoreServer, Task> scrapeServersAndStart = async (core) =>
            {
                CoreServer = core ?? new CoreServer
                {
                    Address = "127.0.0.1",
                    Port = 0,
                    Name = "Unregistered Server"
                };

                //Scrape hosts. Unreachable hosts will be removed
                Logger.Info("Reaching out to other hosts for updated Master Lists...");

                //Commented out is the code that makes this act as a mesh network
                //var hostStatePairs = await HostScraper.ScrapeHosts(State.KnownHosts, settings.ServerName, 0, core);

                //The uncommented duplicate here makes this act as a hub and spoke network, since networkauditor.org is the domain of the master server
                var hostStatePairs = await HostScraper.ScrapeHosts(State.KnownHosts.Where(x => x.Address.Contains("networkauditor")).ToArray(), settings.ServerName, 0, core);

                hostStatePairs = hostStatePairs.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value);
                var newHostList = hostStatePairs.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts).Union(hostStatePairs.Keys);
                State.KnownHosts.AddRange(newHostList);
                var newHosts = State.KnownHosts.Distinct();
                State.KnownHosts.Clear();
                State.KnownHosts.AddRange(newHosts);

                //The current server will always remove itself from its list thanks to it not being up when
                //it starts. Let's fix that. Also, add back the Master Server if it was removed.
                //We accomplish this by triggering the default-on-empty function of GetHosts()
                if (State.KnownHosts.Count == 0) State.KnownHosts.AddRange(config.GetHosts());
                if (core != null) State.KnownHosts.AddRange(State.KnownHosts.Union(new CoreServer[] { core }));

                config.SaveHosts(State.KnownHosts.ToArray());
                Logger.Info("Server list updated.");

                OpenPort(port);

                server = new Server(port);
                server.PacketReceived += Server_PacketReceived;
                server.ClientConnected += Server_ClientConnected;
                server.ClientDisconnected += Server_ClientDisconnected;

#pragma warning disable CS4014
                Task.Run(() => server.Start());
#pragma warning restore CS4014

                //Start a regular check for updates
                Update.PollForUpdates(() =>
                {
                    Logger.Error("A new version is available! The server will now shut down. Please update to continue.");
                    server.Shutdown();
                }, updateCheckToken.Token);
            };

            //Verify that the provided address points to our server
            if (IPAddress.TryParse(address, out var _))
            {
                Logger.Warning($"\'{address}\' seems to be an IP address. You'll need a domain pointed to your server for it to be added to the Master Lists");
                await scrapeServersAndStart(null);
            }
            else if (address != "[serverAddress]")
            {
                Logger.Info("Verifying that \'serverAddress\' points to this server...");

                var connected = new AutoResetEvent(false);
                var keyName = $"{address}:{port}";
                bool verified = false;

                var verificationServer = new Server(port);
                verificationServer.PacketReceived += (_, packet) =>
                {
                    if (packet.Type == PacketType.Connect)
                    {
                        var connect = packet.SpecificPacket as Connect;
                        if (connect.Name == keyName)
                        {
                            verified = true;
                            connected.Set();
                        }
                    }
                };

#pragma warning disable CS4014
                Task.Run(() => verificationServer.Start());
#pragma warning restore CS4014

                var client = new TemporaryClient(address, port, keyName, "0", ConnectTypes.TemporaryConnection);
                client.Start();

                connected.WaitOne(6000);

                client.Shutdown();
                verificationServer.Shutdown();

                if (verified)
                {
                    Logger.Success("Verified address! Server should be added to the Lists of all servers that were scraped for hosts");

                    await scrapeServersAndStart(new CoreServer
                    {
                        Address = address,
                        Port = port,
                        Name = State.ServerSettings.ServerName
                    });
                }
                else
                {
                    Logger.Warning("Failed to verify address. Continuing server startup, but note that this server was not added to the Master Lists, if it wasn't already there");
                    await scrapeServersAndStart(null);
                }
            }
            else
            {
                Logger.Warning("If you provide a value for \'serverAddress\' in the configuration file, your server can be added to the Master Lists");
                await scrapeServersAndStart(null);
            }
        }

        //Courtesy of andruzzzhka's Multiplayer
        private async void OpenPort(int port)
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
                if (State.Players.Any(x => x.Id == client.id.ToString()))
                {
                    var player = State.Players.First(x => x.Id == client.id.ToString());
                    RemovePlayer(player);
                }
                else if (State.Coordinators.Any(x => x.Id == client.id.ToString()))
                {
                    RemoveCoordinator(State.Coordinators.First(x => x.Id == client.id.ToString()));
                }
            }
        }

        private void Server_ClientConnected(ConnectedClient client)
        {
        }

        private static void Log(Packet packet, string suffix)
        {
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
                if (packet.SpecificPacket == null)
                {
                    secondaryInfo = "HEARTBEAT";
                }
                else
                {
                    secondaryInfo = (packet.SpecificPacket as Command).CommandType.ToString();
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                secondaryInfo = (packet.SpecificPacket as Event).Type.ToString();
                if ((packet.SpecificPacket as Event).Type == Event.Types.EventType.PlayerUpdated)
                {
                    var p = (packet.SpecificPacket as Event).ChangedObject.Unpack<Player>();
                    secondaryInfo = $"{secondaryInfo} from ({p.Name} : {p.DownloadState}) : ({p.PlayState} : {p.Score} : {p.StreamDelayMs})";
                }
                else if ((packet.SpecificPacket as Event).Type == Event.Types.EventType.MatchUpdated)
                {
                    secondaryInfo = $"{secondaryInfo} ({((packet.SpecificPacket as Event).ChangedObject.Unpack<Match>()).SelectedDifficulty})";
                }
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                secondaryInfo = $"{(packet.SpecificPacket as ForwardingPacket).SpecificPacket.GetType()}";
            }

            Logger.Debug($"Sending {packet.ToBytes().Length} bytes ({packet.Type}) ({secondaryInfo}){suffix}");
        }

        public void Send(Guid id, Packet packet)
        {
            packet.From = Guid.TryParse(Self?.Id, out var pkt) ? pkt : Guid.Empty;
            server.Send(id, packet.ToBytes());
        }

        public void Send(Guid[] ids, Packet packet)
        {
            #region LOGGING

            Log(packet, $" TO ({string.Join(", ", ids)})");

            #endregion LOGGING

            packet.From = Guid.TryParse(Self?.Id, out var pkt) ? pkt : Guid.Empty;
            server.Send(ids, packet.ToBytes());
        }

        public void ForwardTo(Guid[] ids, Guid from, Packet packet)
        {
            packet.From = from;

            #region LOGGING

            Log(packet, $" TO ({string.Join(", ", ids)}) FROM ({packet.From})");

            #endregion LOGGING

            server.Send(ids, packet.ToBytes());
        }

        private class PacketWrapperJson
        {
            public PacketType Type { get; set; }
            public Dictionary<string, string> SpecificPacket { get; set; }
        }

        public void SendToOverlay(Packet packet)
        {
            if (overlayServer != null)
            {
                //We're assuming the overlay needs JSON, so... Let's convert our serialized class to json
                // var jsonString = JsonSerializer.Serialize(packet, packet.GetType());
                var formatter = new JsonFormatter(new JsonFormatter.Settings(true));

                // Deserialize the serialized packet as a Dictionary<string, string> to pass to the JSON serialization
                var jsonString = JsonConvert.SerializeObject(new PacketWrapperJson
                {
                    Type = packet.Type,
                    SpecificPacket = JsonConvert.DeserializeObject<Dictionary<string, string>>(formatter.Format(packet.SpecificPacket as IMessage))
                });
                Task.Run(() =>
                {
                    try
                    {
                        overlayServer.JsonBroadcast(jsonString);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error sending to overlay:");
                        Logger.Error(e.Message);
                    }
                });
            }
        }

        public void SendToOverlayClient(Guid id, Packet packet)
        {
            if (overlayServer != null)
            {
                //We're assuming the overlay needs JSON, so... Let's convert our serialized class to json
                // var jsonString = JsonSerializer.Serialize(packet, packet.GetType());
                var jsonString = JsonConvert.SerializeObject(packet);
                Task.Run(() =>
                {
                    try
                    {
                        overlayServer.JsonSend(id, jsonString);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error sending to overlay:");
                        Logger.Error(e.Message);
                    }
                });
            }
        }

        private void BroadcastToAllClients(Packet packet, bool toOverlay = true)
        {
            #region LOGGING

            Log(packet, "");

            #endregion LOGGING

            packet.From = Guid.TryParse(Self?.Id, out var gd) ? gd : Guid.NewGuid();
            server.Broadcast(packet.ToBytes());
            if (toOverlay) SendToOverlay(packet);
        }

        #region EventManagement

        public void AddPlayer(Player player)
        {
            lock (State)
            {
                State.Players.Add(player);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerAdded,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(player)
            };
            BroadcastToAllClients(new Packet(@event));

            PlayerConnected?.Invoke(player);
        }

        public void UpdatePlayer(Player player)
        {
            lock (State)
            {
                // TODO: This is garbage
                var newPlayers = State.Players.ToList();
                newPlayers[newPlayers.FindIndex(x => x.Id == player.Id)] = player;
                State.Players.Clear();
                State.Players.AddRange(newPlayers);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerUpdated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(player)
            };
            BroadcastToAllClients(new Packet(@event));

            PlayerInfoUpdated?.Invoke(player);
        }

        public void RemovePlayer(Player player)
        {
            lock (State)
            {
                // TODO: This is garbage
                var newPlayers = State.Players.ToList();
                newPlayers.RemoveAll(x => x.Id == player.Id);
                State.Players.Clear();
                State.Players.AddRange(newPlayers);

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

            var @event = new Event
            {
                Type = Event.Types.EventType.PlayerLeft,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(player)
            };
            BroadcastToAllClients(new Packet(@event));

            PlayerDisconnected?.Invoke(player);
        }

        public void AddCoordinator(Coordinator coordinator)
        {
            lock (State)
            {
                State.Coordinators.Add(coordinator);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.CoordinatorAdded,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(coordinator)
            };
            BroadcastToAllClients(new Packet(@event));
        }

        public void RemoveCoordinator(Coordinator coordinator)
        {
            lock (State)
            {
                // TODO: This is garbage
                var newCoordinators = State.Coordinators.ToList();
                newCoordinators.RemoveAll(x => x.Id == coordinator.Id);
                State.Coordinators.Clear();
                State.Coordinators.AddRange(newCoordinators);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.CoordinatorLeft,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(coordinator)
            };
            BroadcastToAllClients(new Packet(@event));
        }

        public void CreateMatch(Match match)
        {
            lock (State)
            {
                State.Matches.Add(match);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.MatchCreated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(match)
            };
            BroadcastToAllClients(new Packet(@event));

            MatchCreated?.Invoke(match);
        }

        public void UpdateMatch(Match match)
        {
            lock (State)
            {
                // TODO: This is garbage
                var newMatches = State.Matches.ToList();
                newMatches[newMatches.FindIndex(x => x.Guid == match.Guid)] = match;
                State.Matches.Clear();
                State.Matches.AddRange(newMatches);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.MatchUpdated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(match)
            };

            var updatePacket = new Packet(@event);

            BroadcastToAllClients(updatePacket);

            MatchInfoUpdated?.Invoke(match);
        }

        public void DeleteMatch(Match match)
        {
            lock (State)
            {
                // TODO: This is garbage
                var newMatches = State.Matches.ToList();
                newMatches.RemoveAll(x => x.Guid == match.Guid);
                State.Matches.Clear();
                State.Matches.AddRange(newMatches);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.MatchDeleted,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(match)
            };
            BroadcastToAllClients(new Packet(@event));

            MatchDeleted?.Invoke(match);
        }

        public async Task<Response> SendCreateQualifierEvent(CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host == CoreServer)
            {
                return CreateQualifierEvent(qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet(new Event
                {
                    Type = Event.Types.EventType.QualifierEventCreated,
                    ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
                }), typeof(Response), $"{CoreServer.Address}:{CoreServer.Port}", 0);
                return result?.SpecificPacket as Response ?? new Response
                {
                    Type = ResponseType.Fail,
                    Message = "The request to the designated server timed out. The server is offline or otherwise unreachable"
                };
            }
        }

        public async Task<Response> SendUpdateQualifierEvent(CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host == CoreServer)
            {
                return UpdateQualifierEvent(qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet(new Event
                {
                    Type = Event.Types.EventType.QualifierEventUpdated,
                    ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
                }), typeof(Response), $"{CoreServer.Address}:{CoreServer.Port}", 0);
                return result?.SpecificPacket as Response ?? new Response
                {
                    Type = ResponseType.Fail,
                    Message = "The request to the designated server timed out. The server is offline or otherwise unreachable"
                };
            }
        }

        public async Task<Response> SendDeleteQualifierEvent(CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host == CoreServer)
            {
                return DeleteQualifierEvent(qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet(new Event
                {
                    Type = Event.Types.EventType.QualifierEventDeleted,
                    ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
                }), typeof(Response), $"{CoreServer.Address}:{CoreServer.Port}", 0);
                return result?.SpecificPacket as Response ?? new Response
                {
                    Type = ResponseType.Fail,
                    Message = "The request to the designated server timed out. The server is offline or otherwise unreachable"
                };
            }
        }

        public Response CreateQualifierEvent(QualifierEvent qualifierEvent)
        {
            if (Database.Events.Any(x => !x.Old && x.GuildId == qualifierEvent.Guild.Id))
            {
                return new Response
                {
                    Type = ResponseType.Fail,
                    Message = "There is already an event running for your guild"
                };
            }

            var databaseEvent = Database.ConvertModelToEventDatabase(qualifierEvent);
            Database.Events.Add(databaseEvent);
            Database.SaveChanges();

            lock (State)
            {
                State.Events.Add(qualifierEvent);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.QualifierEventCreated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
            };
            BroadcastToAllClients(new Packet(@event));

            return new Response
            {
                Type = ResponseType.Success,
                Message = $"Successfully created event: {databaseEvent.Name} with settings: {(QualifierEvent.Types.EventSettings)databaseEvent.Flags}"
            };
        }

        public Response UpdateQualifierEvent(QualifierEvent qualifierEvent)
        {
            if (!Database.Events.Any(x => !x.Old && x.GuildId == qualifierEvent.Guild.Id))
            {
                return new Response
                {
                    Type = ResponseType.Fail,
                    Message = "There is not an event running for your guild"
                };
            }

            //Update Event entry
            var newDatabaseEvent = Database.ConvertModelToEventDatabase(qualifierEvent);
            Database.Entry(Database.Events.First(x => x.EventId == qualifierEvent.EventId.ToString())).CurrentValues.SetValues(newDatabaseEvent);

            //Check for removed songs
            foreach (var song in Database.Songs.Where(x => x.EventId == qualifierEvent.EventId.ToString() && !x.Old))
            {
                if (!qualifierEvent.QualifierMaps.Any(x => song.LevelId == x.Beatmap.LevelId &&
                    song.Characteristic == x.Beatmap.Characteristic.SerializedName &&
                    song.BeatmapDifficulty == (int)x.Beatmap.Difficulty &&
                    song.GameOptions == (int)x.GameplayModifiers.Options &&
                    song.PlayerOptions == (int)x.PlayerSettings.Options))
                {
                    song.Old = true;
                }
            }

            //Check for newly added songs
            foreach (var song in qualifierEvent.QualifierMaps)
            {
                if (!Database.Songs.Any(x => !x.Old &&
                    x.LevelId == song.Beatmap.LevelId &&
                    x.Characteristic == song.Beatmap.Characteristic.SerializedName &&
                    x.BeatmapDifficulty == (int)song.Beatmap.Difficulty &&
                    x.GameOptions == (int)song.GameplayModifiers.Options &&
                    x.PlayerOptions == (int)song.PlayerSettings.Options))
                {
                    Database.Songs.Add(new Discord.Database.Song
                    {
                        EventId = qualifierEvent.EventId.ToString(),
                        LevelId = song.Beatmap.LevelId,
                        Name = song.Beatmap.Name,
                        Characteristic = song.Beatmap.Characteristic.SerializedName,
                        BeatmapDifficulty = (int)song.Beatmap.Difficulty,
                        GameOptions = (int)song.GameplayModifiers.Options,
                        PlayerOptions = (int)song.PlayerSettings.Options
                    });
                }
            }

            Database.SaveChanges();

            lock (State)
            {
                // TODO: This is garbage
                var newEvents = State.Events.ToList();
                newEvents[newEvents.FindIndex(x => x.EventId == qualifierEvent.EventId)] = qualifierEvent;
                State.Events.Clear();
                State.Events.AddRange(newEvents);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.QualifierEventUpdated,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
            };

            var updatePacket = new Packet(@event);

            BroadcastToAllClients(updatePacket);

            return new Response
            {
                Type = ResponseType.Success,
                Message = $"Successfully updated event: {newDatabaseEvent.Name}"
            };
        }

        public Response DeleteQualifierEvent(QualifierEvent qualifierEvent)
        {
            if (!Database.Events.Any(x => !x.Old && x.GuildId == qualifierEvent.Guild.Id))
            {
                return new Response
                {
                    Type = ResponseType.Fail,
                    Message = "There is not an event running for your guild"
                };
            }

            //Mark all songs and scores as old
            Database.Events.Where(x => x.EventId == qualifierEvent.EventId.ToString()).ForEachAsync(x => x.Old = true);
            Database.Songs.Where(x => x.EventId == qualifierEvent.EventId.ToString()).ForEachAsync(x => x.Old = true);
            Database.Scores.Where(x => x.EventId == qualifierEvent.EventId.ToString()).ForEachAsync(x => x.Old = true);
            Database.SaveChanges();

            lock (State)
            {
                // TODO: This is garbage
                var newEvents = State.Events.ToList();
                newEvents.RemoveAll(x => x.EventId == qualifierEvent.EventId);
                State.Events.Clear();
                State.Events.AddRange(newEvents);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.QualifierEventDeleted,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(qualifierEvent)
            };
            BroadcastToAllClients(new Packet(@event));

            return new Response
            {
                Type = ResponseType.Success,
                Message = $"Successfully ended event: {qualifierEvent.Name}"
            };
        }

        public void AddHost(CoreServer host)
        {
            lock (State)
            {
                // TODO: This is ESPECIALLY garbage
                State.KnownHosts.Add(host);
                var newHosts = State.KnownHosts.Distinct(); //hashset prevents duplicates
                State.KnownHosts.Clear();
                State.KnownHosts.AddRange(newHosts);

                //Save to disk
                config.SaveHosts(State.KnownHosts.ToArray());
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.HostAdded,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(host)
            };
            BroadcastToAllClients(new Packet(@event));
        }

        public void RemoveHost(CoreServer host)
        {
            lock (State)
            {
                // TODO: This is garbage (and also doesn't save to disk properly)
                var newHosts = State.KnownHosts.ToList();
                newHosts.Remove(host);
                State.KnownHosts.Clear();
                State.KnownHosts.AddRange(newHosts);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                Type = Event.Types.EventType.HostRemoved,
                ChangedObject = Google.Protobuf.WellKnownTypes.Any.Pack(host)
            };
            BroadcastToAllClients(new Packet(@event));
        }

        #endregion EventManagement

        private void Server_PacketReceived(ConnectedClient player, Packet packet)
        {
            #region LOGGING

            Log(packet, "");

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
                        Response = new Response
                        {
                            Type = ResponseType.Fail,
                            Message = $"Version mismatch, this server is on version {SharedConstructs.Version}"
                        },
                        State = null,
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
                else if (connect.ClientType == ConnectTypes.Player)
                {
                    var newPlayer = new Player()
                    {
                        Id = player.id.ToString(),
                        Name = connect.Name,
                        UserId = connect.UserId,
                        Team = new Team() { Id = Guid.Empty.ToString(), Name = "None" }
                    };

                    AddPlayer(newPlayer);

                    //Give the newly connected player their Self and State
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Response = new Response
                        {
                            Type = ResponseType.Success,
                            Message = $"Connected to {settings.ServerName}!"
                        },
                        Player = newPlayer,
                        State = State,
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
                else if (connect.ClientType == ConnectTypes.Coordinator)
                {
                    if (connect.Password == settings.Password)
                    {
                        var coordinator = new Coordinator()
                        {
                            Id = player.id.ToString(),
                            Name = connect.Name
                        };
                        AddCoordinator(coordinator);

                        //Give the newly connected coordinator their Self and State
                        Send(player.id, new Packet(new ConnectResponse()
                        {
                            Response = new Response
                            {
                                Type = ResponseType.Success,
                                Message = $"Connected to {settings.ServerName}!"
                            },
                            Coordinator = coordinator,
                            State = State,
                            ServerVersion = SharedConstructs.VersionCode
                        }));
                    }
                    else
                    {
                        Send(player.id, new Packet(new ConnectResponse()
                        {
                            Response = new Response
                            {
                                Type = ResponseType.Fail,
                                Message = $"Incorrect password for {settings.ServerName}!"
                            },
                            State = State,
                            ServerVersion = SharedConstructs.VersionCode
                        }));
                    }
                }
                else if (connect.ClientType == ConnectTypes.TemporaryConnection)
                {
                    //A scraper just wants a copy of our state, so let's give it to them
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Response = new Response
                        {
                            Type = ResponseType.Success,
                            Message = $"Connected to {settings.ServerName} (scraper)!",
                        },
                        State = State,
                        ServerVersion = SharedConstructs.VersionCode
                    }));
                }
            }
            else if (packet.Type == PacketType.ScoreRequest)
            {
                ScoreRequest request = packet.SpecificPacket as ScoreRequest;

                var scores = Database.Scores
                    .Where(x => x.EventId == request.EventId.ToString() &&
                        x.LevelId == request.Parameters.Beatmap.LevelId &&
                        x.Characteristic == request.Parameters.Beatmap.Characteristic.SerializedName &&
                        x.BeatmapDifficulty == (int)request.Parameters.Beatmap.Difficulty &&
                        x.GameOptions == (int)request.Parameters.GameplayModifiers.Options &&
                        //x.PlayerOptions == (int)request.Parameters.PlayerSettings.Options &&
                        !x.Old).OrderByDescending(x => x._Score)
                    .Select(x => new Score
                    {
                        EventId = request.EventId,
                        Parameters = request.Parameters,
                        Username = x.Username,
                        UserId = x.UserId,
                        Score_ = x._Score,
                        FullCombo = x.FullCombo,
                        Color = x.Username == "Moon" ? "#00ff00" : "#ffffff"
                    });

                //If scores are disabled for this event, don't return them
                var @event = Database.Events.FirstOrDefault(x => x.EventId == request.EventId.ToString());
                if (((QualifierEvent.Types.EventSettings)@event.Flags).HasFlag(QualifierEvent.Types.EventSettings.HideScoresFromPlayers))
                {
                    Send(player.id, new Packet(new ScoreRequestResponse()));
                }
                else
                {
                    var pkt = new ScoreRequestResponse();
                    pkt.Scores.AddRange(scores);
                    Send(player.id, new Packet(pkt));
                }
            }
            else if (packet.Type == PacketType.SubmitScore)
            {
                SubmitScore submitScore = packet.SpecificPacket as SubmitScore;

                //Check to see if the song exists in the database
                var song = Database.Songs.FirstOrDefault(x => x.EventId == submitScore.Score.EventId.ToString() &&
                        x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                        x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                        x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                        x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                        //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                        !x.Old);

                if (song != null)
                {
                    //Mark all older scores as old
                    var scores = Database.Scores
                        .Where(x => x.EventId == submitScore.Score.EventId.ToString() &&
                            x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                            x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                            x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                            x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                            //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                            !x.Old &&
                            x.UserId == submitScore.Score.UserId);

                    var oldHighScore = (scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? 0);

                    if ((scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? 0) < submitScore.Score.Score_)
                    {
                        scores.ForEach(x => x.Old = true);

                        Database.Scores.Add(new Discord.Database.Score
                        {
                            EventId = submitScore.Score.EventId.ToString(),
                            UserId = submitScore.Score.UserId,
                            Username = submitScore.Score.Username,
                            LevelId = submitScore.Score.Parameters.Beatmap.LevelId,
                            Characteristic = submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName,
                            BeatmapDifficulty = (int)submitScore.Score.Parameters.Beatmap.Difficulty,
                            GameOptions = (int)submitScore.Score.Parameters.GameplayModifiers.Options,
                            PlayerOptions = (int)submitScore.Score.Parameters.PlayerSettings.Options,
                            _Score = submitScore.Score.Score_,
                            FullCombo = submitScore.Score.FullCombo,
                        });
                        Database.SaveChanges();
                    }

                    var newScores = Database.Scores
                        .Where(x => x.EventId == submitScore.Score.EventId.ToString() &&
                            x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                            x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                            x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                            x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                            //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                            !x.Old).OrderByDescending(x => x._Score).Take(10)
                        .Select(x => new Score
                        {
                            EventId = submitScore.Score.EventId,
                            Parameters = submitScore.Score.Parameters,
                            Username = x.Username,
                            UserId = x.UserId,
                            Score_ = x._Score,
                            FullCombo = x.FullCombo,
                            Color = "#ffffff"
                        });

                    //Return the new scores for the song so the leaderboard will update immediately
                    //If scores are disabled for this event, don't return them
                    var @event = Database.Events.FirstOrDefault(x => x.EventId == submitScore.Score.EventId.ToString());
                    var returnScores = ((QualifierEvent.Types.EventSettings)@event.Flags).HasFlag(QualifierEvent.Types.EventSettings.HideScoresFromPlayers);
                    var spkt = new ScoreRequestResponse();
                    spkt.Scores.AddRange(returnScores ? newScores : Array.Empty<Score>());
                    Send(player.id, new Packet(spkt));
                    SendToOverlay(new Packet(spkt));
                    if (@event.InfoChannelId != default && returnScores && QualifierBot != null)
                    {
                        QualifierBot.SendScoreEvent(@event.InfoChannelId, submitScore);
                    }
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                switch (@event.Type)
                {
                    case Event.Types.EventType.CoordinatorAdded:
                        AddCoordinator(@event.ChangedObject.Unpack<Coordinator>());
                        break;

                    case Event.Types.EventType.CoordinatorLeft:
                        RemoveCoordinator(@event.ChangedObject.Unpack<Coordinator>());
                        break;

                    case Event.Types.EventType.MatchCreated:
                        CreateMatch(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.MatchUpdated:
                        UpdateMatch(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.MatchDeleted:
                        DeleteMatch(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.PlayerAdded:
                        AddPlayer(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.PlayerUpdated:
                        UpdatePlayer(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.PlayerLeft:
                        RemovePlayer(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.QualifierEventCreated:
                        Send(player.id, new Packet(CreateQualifierEvent(@event.ChangedObject.Unpack<QualifierEvent>())));
                        break;

                    case Event.Types.EventType.QualifierEventUpdated:
                        Send(player.id, new Packet(UpdateQualifierEvent(@event.ChangedObject.Unpack<QualifierEvent>())));
                        break;

                    case Event.Types.EventType.QualifierEventDeleted:
                        Send(player.id, new Packet(DeleteQualifierEvent(@event.ChangedObject.Unpack<QualifierEvent>())));
                        break;

                    case Event.Types.EventType.HostAdded:
                        AddHost(@event.ChangedObject.Unpack<CoreServer>());
                        break;

                    case Event.Types.EventType.HostRemoved:
                        RemoveHost(@event.ChangedObject.Unpack<CoreServer>());
                        break;

                    default:
                        Logger.Error($"Unknown command received from {player.id}!");
                        break;
                }
            }
            else if (packet.Type == PacketType.SongFinished)
            {
                BroadcastToAllClients(packet, false);
                PlayerFinishedSong?.Invoke(packet.SpecificPacket as SongFinished);
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                var forwardingPacket = packet.SpecificPacket as ForwardingPacket;
                var forwardedPacket = new Packet(forwardingPacket.SpecificPacket);

                //TODO: REMOVE
                /*var scoreboardClient = State.Coordinators.FirstOrDefault(x => x.Name == "[Scoreboard]");
                if (scoreboardClient != null) forwardingPacket.ForwardTo = forwardingPacket.ForwardTo.ToList().Union(new Guid[] { scoreboardClient.Id }).ToArray();*/

                ForwardTo(forwardingPacket.ForwardTo.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).ToArray(), packet.From, forwardedPacket);
            }
        }

        private void overlay_PacketReceived(ConnectedClient player, Packet packet)
        {
            SendToOverlay(packet);
            if (packet.Type == PacketType.Acknowledgement)
            {
                Acknowledgement acknowledgement = packet.SpecificPacket as Acknowledgement;
                AckReceived?.Invoke(acknowledgement, packet.From);
            }
            else if (packet.Type == PacketType.Connect)
            {
                Connect connect = packet.SpecificPacket as Connect;

                if (connect.ClientType == ConnectTypes.Coordinator)
                {
                    if (connect.Password == settings.Password)
                    {
                        var coordinator = new Coordinator()
                        {
                            Id = player.id.ToString(),
                            Name = connect.Name,
                            UserId = connect.UserId
                        };
                        AddCoordinator(coordinator);

                        //Give the newly connected coordinator their Self and State
                        SendToOverlayClient(player.id, new Packet(new ConnectResponse()
                        {
                            Response = new Response
                            {
                                Type = ResponseType.Success,
                                Message = $"Connected to {settings.ServerName}!"
                            },
                            Coordinator = coordinator,
                            State = State,
                            ServerVersion = SharedConstructs.VersionCode
                        }));
                    }
                    else
                    {
                        SendToOverlayClient(player.id, new Packet(new ConnectResponse()
                        {
                            Response = new Response
                            {
                                Type = ResponseType.Fail,
                                Message = $"Incorrect password for {settings.ServerName}!"
                            },
                            State = State,
                            ServerVersion = SharedConstructs.VersionCode
                        }));
                    }
                }
            }
            else if (packet.Type == PacketType.ScoreRequest)
            {
                ScoreRequest request = packet.SpecificPacket as ScoreRequest;

                var scores = Database.Scores
                    .Where(x => x.EventId == request.EventId.ToString() &&
                        x.LevelId == request.Parameters.Beatmap.LevelId &&
                        x.Characteristic == request.Parameters.Beatmap.Characteristic.SerializedName &&
                        x.BeatmapDifficulty == (int)request.Parameters.Beatmap.Difficulty &&
                        x.GameOptions == (int)request.Parameters.GameplayModifiers.Options &&
                        //x.PlayerOptions == (int)request.Parameters.PlayerSettings.Options &&
                        !x.Old).OrderByDescending(x => x._Score).Take(10)
                    .Select(x => new Score
                    {
                        EventId = request.EventId,
                        Parameters = request.Parameters,
                        Username = x.Username,
                        UserId = x.UserId,
                        Score_ = x._Score,
                        FullCombo = x.FullCombo,
                        Color = x.Username == "Moon" ? "#00ff00" : "#ffffff"
                    });

                //If scores are disabled for this event, don't return them
                var @event = Database.Events.FirstOrDefault(x => x.EventId == request.EventId.ToString());
                if (((QualifierEvent.Types.EventSettings)@event.Flags).HasFlag(QualifierEvent.Types.EventSettings.HideScoresFromPlayers))
                {
                    SendToOverlayClient(player.id, new Packet(new ScoreRequestResponse()));
                }
                else
                {
                    var pkt = new ScoreRequestResponse();
                    pkt.Scores.AddRange(scores);
                    SendToOverlayClient(player.id, new Packet(pkt));
                }
            }
            else if (packet.Type == PacketType.SubmitScore)
            {
                SubmitScore submitScore = packet.SpecificPacket as SubmitScore;

                //Check to see if the song exists in the database
                var song = Database.Songs.FirstOrDefault(x => x.EventId == submitScore.Score.EventId.ToString() &&
                        x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                        x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                        x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                        x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                        //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                        !x.Old);

                if (song != null)
                {
                    //Mark all older scores as old
                    var scores = Database.Scores
                        .Where(x => x.EventId == submitScore.Score.EventId.ToString() &&
                            x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                            x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                            x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                            x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                            //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                            !x.Old &&
                            x.UserId == submitScore.Score.UserId);

                    var oldHighScore = (scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? 0);

                    if ((scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? 0) < submitScore.Score.Score_)
                    {
                        scores.ForEach(x => x.Old = true);

                        Database.Scores.Add(new Discord.Database.Score
                        {
                            EventId = submitScore.Score.EventId.ToString(),
                            UserId = submitScore.Score.UserId,
                            Username = submitScore.Score.Username,
                            LevelId = submitScore.Score.Parameters.Beatmap.LevelId,
                            Characteristic = submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName,
                            BeatmapDifficulty = (int)submitScore.Score.Parameters.Beatmap.Difficulty,
                            GameOptions = (int)submitScore.Score.Parameters.GameplayModifiers.Options,
                            PlayerOptions = (int)submitScore.Score.Parameters.PlayerSettings.Options,
                            _Score = submitScore.Score.Score_,
                            FullCombo = submitScore.Score.FullCombo,
                        });
                        Database.SaveChanges();
                    }

                    var newScores = Database.Scores
                        .Where(x => x.EventId == submitScore.Score.EventId.ToString() &&
                            x.LevelId == submitScore.Score.Parameters.Beatmap.LevelId &&
                            x.Characteristic == submitScore.Score.Parameters.Beatmap.Characteristic.SerializedName &&
                            x.BeatmapDifficulty == (int)submitScore.Score.Parameters.Beatmap.Difficulty &&
                            x.GameOptions == (int)submitScore.Score.Parameters.GameplayModifiers.Options &&
                            //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                            !x.Old).OrderByDescending(x => x._Score).Take(10)
                        .Select(x => new Score
                        {
                            EventId = submitScore.Score.EventId,
                            Parameters = submitScore.Score.Parameters,
                            Username = x.Username,
                            UserId = x.UserId,
                            Score_ = x._Score,
                            FullCombo = x.FullCombo,
                            Color = "#ffffff"
                        });

                    //Return the new scores for the song so the leaderboard will update immediately
                    //If scores are disabled for this event, don't return them
                    var @event = Database.Events.FirstOrDefault(x => x.EventId == submitScore.Score.EventId.ToString());
                    if (((QualifierEvent.Types.EventSettings)@event.Flags).HasFlag(QualifierEvent.Types.EventSettings.HideScoresFromPlayers))
                    {
                        SendToOverlayClient(player.id, new Packet(new ScoreRequestResponse()));
                    }
                    else
                    {
                        var pkt = new ScoreRequestResponse();
                        pkt.Scores.AddRange(newScores);
                        SendToOverlayClient(player.id, new Packet(pkt));
                    }
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event; ;
                switch (@event.Type)
                {
                    case Event.Types.EventType.CoordinatorAdded:
                        AddCoordinator(@event.ChangedObject.Unpack<Coordinator>());
                        break;

                    case Event.Types.EventType.CoordinatorLeft:
                        RemoveCoordinator(@event.ChangedObject.Unpack<Coordinator>());
                        break;

                    case Event.Types.EventType.MatchCreated:
                        CreateMatch(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.MatchUpdated:
                        UpdateMatch(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.MatchDeleted:
                        DeleteMatch(@event.ChangedObject.Unpack<Match>());
                        break;

                    case Event.Types.EventType.PlayerAdded:
                        AddPlayer(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.PlayerUpdated:
                        UpdatePlayer(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.PlayerLeft:
                        RemovePlayer(@event.ChangedObject.Unpack<Player>());
                        break;

                    case Event.Types.EventType.QualifierEventCreated:
                        Send(player.id, new Packet(CreateQualifierEvent(@event.ChangedObject.Unpack<QualifierEvent>())));
                        break;

                    case Event.Types.EventType.QualifierEventUpdated:
                        Send(player.id, new Packet(UpdateQualifierEvent(@event.ChangedObject.Unpack<QualifierEvent>())));
                        break;

                    case Event.Types.EventType.QualifierEventDeleted:
                        Send(player.id, new Packet(DeleteQualifierEvent(@event.ChangedObject.Unpack<QualifierEvent>())));
                        break;

                    case Event.Types.EventType.HostAdded:
                        AddHost(@event.ChangedObject.Unpack<CoreServer>());
                        break;

                    case Event.Types.EventType.HostRemoved:
                        RemoveHost(@event.ChangedObject.Unpack<CoreServer>());
                        break;

                    default:
                        Logger.Error($"Unknown command received from {player.id}!");
                        break;
                }
            }
            else if (packet.Type == PacketType.SongFinished)
            {
                BroadcastToAllClients(packet, false);
                PlayerFinishedSong?.Invoke(packet.SpecificPacket as SongFinished);
            }
            else if (packet.Type == PacketType.ForwardingPacket)
            {
                var forwardingPacket = packet.SpecificPacket as ForwardingPacket;
                var forwardedPacket = new Packet(forwardingPacket.SpecificPacket);

                //TODO: REMOVE
                /*var scoreboardClient = State.Coordinators.FirstOrDefault(x => x.Name == "[Scoreboard]");
                if (scoreboardClient != null) forwardingPacket.ForwardTo = forwardingPacket.ForwardTo.ToList().Union(new Guid[] { scoreboardClient.Id }).ToArray();*/

                ForwardTo(forwardingPacket.ForwardTo.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).ToArray(), packet.From, forwardedPacket);
            }
        }
    }
}