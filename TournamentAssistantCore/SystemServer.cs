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
using TournamentAssistantCore.Discord;
using TournamentAssistantCore.Discord.Helpers;
using TournamentAssistantCore.Discord.Services;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.Packets.Response;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;
using static TournamentAssistantShared.Packet;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantCore
{
    public class SystemServer : IConnection, INotifyPropertyChanged
    {
        Server server;
        WsServer overlayServer;

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
                        Id = Guid.NewGuid(),
                        Name = "Team Green"
                    },
                    new Team()
                    {
                        Id = Guid.NewGuid(),
                        Name = "Team Spicy"
                    }
                };
                config.SaveTeams(teamsValue);
            }

            settings = new ServerSettings();
            settings.ServerName = nameValue;
            settings.Password = passwordValue;
            settings.EnableTeams = enableTeamsValue;
            settings.Teams = teamsValue;
            settings.ScoreUpdateFrequency = Convert.ToInt32(scoreUpdateFrequencyValue);
            settings.BannedMods = bannedModsValue;

            address = addressValue;
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
            State.KnownHosts = config.GetHosts();

            Logger.Info($"Running on {AutoUpdater.osType}");

            //Check for updates
            Logger.Info("Checking for updates...");
            var newVersion = await Update.GetLatestRelease();
            /*if (System.Version.Parse(SharedConstructs.Version) < newVersion)
            {
                Logger.Error($"Update required! You are on \'{SharedConstructs.Version}\', new version is \'{newVersion}\'");
                Logger.Info("Attempting AutoUpdate...");
                bool UpdateSuccess = await AutoUpdater.AttemptAutoUpdate();
                if (!UpdateSuccess)
                {
                    Logger.Error("AutoUpdate Failed. Please Update Manually. Shutting down");
                    //Moon's note / TODO: Can't do this from shared. Screw the threads
                    //SystemHost.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    Environment.Exit(0);
                }
                else
                {
                    Logger.Warning("Update was successful, exitting...");
                    //SystemHost.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    Environment.Exit(0);
                }
            }
            else Logger.Success($"You are on the most recent version! ({SharedConstructs.Version})");
            */
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
            State.Events = events.Select(x => Database.ConvertDatabaseToModel(getSongsForEvent(x.EventId).ToArray(), x)).ToArray();

            //Give our new server a sense of self :P
            Self = new Coordinator()
            {
                Id = Guid.Empty,
                Name = "HOST"
            };

            async Task scrapeServersAndStart(CoreServer core)
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
                State.KnownHosts = newHostList.ToArray();

                //The current server will always remove itself from its list thanks to it not being up when
                //it starts. Let's fix that. Also, add back the Master Server if it was removed.
                //We accomplish this by triggering the default-on-empty function of GetHosts()
                if (State.KnownHosts.Length == 0) State.KnownHosts = config.GetHosts();
                if (core != null) State.KnownHosts = State.KnownHosts.Union(new CoreServer[] { core }).ToArray();

                config.SaveHosts(State.KnownHosts);
                Logger.Info("Server list updated.");

                OpenPort(port);

                server = new Server(port);
                server.PacketReceived += Server_PacketReceived;
                server.ClientConnected += Server_ClientConnected;
                server.ClientDisconnected += Server_ClientDisconnected;

                server.Start();

                //Start a regular check for updates
                Update.PollForUpdates(() =>
                { 
                    server.Shutdown();
                    //SystemHost.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    Environment.Exit(0);
                }, updateCheckToken.Token);
            };

            //Verify that the provided address points to our server
            if (IPAddress.TryParse(address, out _))
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

                verificationServer.Start();

                var client = new TemporaryClient(address, port, keyName, "0", Connect.ConnectTypes.TemporaryConnection);
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
                Logger.Warning($"Can't open port {port} using UPnP! (This is only relevant for people behind NAT who don't port forward. If you're being hosted by an actual server, or you've set up port forwarding manually, you can safely ignore this message. As well as any other yellow messages... Yellow means \"warning\" folks.");
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
                // var jsonString = JsonSerializer.Serialize(packet, packet.GetType());
                var jsonString = JsonConvert.SerializeObject(packet);
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
            if(toOverlay) SendToOverlay(packet);
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
                    Type = Event.EventType.QualifierEventCreated,
                    ChangedObject = qualifierEvent
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
                    Type = Event.EventType.QualifierEventUpdated,
                    ChangedObject = qualifierEvent
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
                    Type = Event.EventType.QualifierEventDeleted,
                    ChangedObject = qualifierEvent
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
                var newEvents = State.Events.ToList();
                newEvents.Add(qualifierEvent);
                State.Events = newEvents.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.Type = Event.EventType.QualifierEventCreated;
            @event.ChangedObject = qualifierEvent;
            BroadcastToAllClients(new Packet(@event));

            return new Response
            {
                Type = ResponseType.Success,
                Message = $"Successfully created event: {databaseEvent.Name} with settings: {(QualifierEvent.EventSettings)databaseEvent.Flags}"
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
                var newEvents = State.Events.ToList();
                newEvents.RemoveAll(x => x.EventId == qualifierEvent.EventId);
                State.Events = newEvents.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.Type = Event.EventType.QualifierEventDeleted;
            @event.ChangedObject = qualifierEvent;
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
                var newHosts = State.KnownHosts.ToHashSet(); //hashset prevents duplicates
                newHosts.Add(host);
                State.KnownHosts = newHosts.ToArray();

                //Save to disk
                config.SaveHosts(State.KnownHosts);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.Type = Event.EventType.HostAdded;
            @event.ChangedObject = host;
            BroadcastToAllClients(new Packet(@event));
        }

        public void RemoveHost(CoreServer host)
        {
            lock (State)
            {
                var newHosts = State.KnownHosts.ToList();
                newHosts.Remove(host);
                State.KnownHosts = newHosts.ToArray();
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event();
            @event.Type = Event.EventType.HostRemoved;
            @event.ChangedObject = host;
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

                if (connect.ClientVersion != VersionCode)
                {
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Type = ResponseType.Fail,
                        Self = null,
                        State = null,
                        Message = $"Version mismatch, this server is on version {SharedConstructs.Version}",
                        ServerVersion = VersionCode
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
                        Type = ResponseType.Success,
                        Self = newPlayer,
                        State = State,
                        Message = $"Connected to {settings.ServerName}!",
                        ServerVersion = VersionCode
                    }));
                }
                else if (connect.ClientType == Connect.ConnectTypes.Coordinator)
                {
                    if (string.IsNullOrWhiteSpace(settings.Password) || connect.Password == settings.Password)
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
                            Type = ResponseType.Success,
                            Self = coordinator,
                            State = State,
                            Message = $"Connected to {settings.ServerName}!",
                            ServerVersion = VersionCode
                        }));
                    }
                    else
                    {
                        Send(player.id, new Packet(new ConnectResponse()
                        {
                            Type = ResponseType.Fail,
                            State = State,
                            Message = $"Incorrect password for {settings.ServerName}!",
                            ServerVersion = VersionCode
                        }));
                    }
                }
                else if (connect.ClientType == Connect.ConnectTypes.TemporaryConnection)
                {
                    //A scraper just wants a copy of our state, so let's give it to them
                    Send(player.id, new Packet(new ConnectResponse()
                    {
                        Type = ResponseType.Success,
                        Self = null,
                        State = State,
                        Message = $"Connected to {settings.ServerName} (scraper)!",
                        ServerVersion = VersionCode
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
                    _Score = x._Score,
                    FullCombo = x.FullCombo,
                    Color = x.Username == "Moon" ? "#00ff00" : "#ffffff"
                });

                //If scores are disabled for this event, don't return them
                var @event = Database.Events.FirstOrDefault(x => x.EventId == request.EventId.ToString());
                if (((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.HideScoreFromPlayers))
                {
                    Send(player.id, new Packet(new ScoreRequestResponse
                    {
                        Scores = new Score[] { }
                    }));
                }
                else
                {
                    Send(player.id, new Packet(new ScoreRequestResponse
                    {
                        Scores = scores.ToArray()
                    }));
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

                    var oldHighScore = (scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? -1);
                    if (oldHighScore < submitScore.Score._Score)
                    {
                        foreach (var score in scores) score.Old = true;

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
                            _Score = submitScore.Score._Score,
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
                            _Score = x._Score,
                            FullCombo = x.FullCombo,
                            Color = "#ffffff"
                        });

                    //Return the new scores for the song so the leaderboard will update immediately
                    //If scores are disabled for this event, don't return them
                    var @event = Database.Events.FirstOrDefault(x => x.EventId == submitScore.Score.EventId.ToString());
                    var hideScores = ((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.HideScoreFromPlayers);
                    var enableLeaderboardMessage = ((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.EnableLeaderboardMessage);

                    Send(player.id, new Packet(new ScoreRequestResponse
                    {
                        Scores = hideScores ? new Score[] { } : newScores.ToArray()
                    }));
                    SendToOverlay(new Packet(new ScoreRequestResponse
                    {
                        Scores = hideScores ? new Score[] { } : newScores.ToArray()
                    }));

                    if (oldHighScore < submitScore.Score._Score && @event.InfoChannelId != default && !hideScores && QualifierBot != null)
                    {
                        QualifierBot.SendScoreEvent(@event.InfoChannelId, submitScore);

                        if (enableLeaderboardMessage)
                        {
                            var eventSongs = Database.Songs.Where(x => x.EventId == submitScore.Score.EventId.ToString() && !x.Old);
                            var eventScores = Database.Scores.Where(x => x.EventId == submitScore.Score.EventId.ToString() && !x.Old);
                            Task.Run(async () =>
                            {
                                var newMessageId = await QualifierBot.SendLeaderboardUpdate(@event.InfoChannelId, @event.LeaderboardMessageId, eventScores.ToList(), eventSongs.ToList());
                                if (@event.LeaderboardMessageId != newMessageId)
                                {
                                    @event.LeaderboardMessageId = newMessageId;
                                    await Database.SaveChangesAsync();
                                }
                            });
                        }
                    }
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
                        Send(player.id, new Packet(CreateQualifierEvent(@event.ChangedObject as QualifierEvent)));
                        break;
                    case Event.EventType.QualifierEventUpdated:
                        Send(player.id, new Packet(UpdateQualifierEvent(@event.ChangedObject as QualifierEvent)));
                        break;
                    case Event.EventType.QualifierEventDeleted:
                        Send(player.id, new Packet(DeleteQualifierEvent(@event.ChangedObject as QualifierEvent)));
                        break;
                    case Event.EventType.HostAdded:
                        AddHost(@event.ChangedObject as CoreServer);
                        break;
                    case Event.EventType.HostRemoved:
                        RemoveHost(@event.ChangedObject as CoreServer);
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

                ForwardTo(forwardingPacket.ForwardTo, packet.From, forwardedPacket);
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

                if (connect.ClientType == Connect.ConnectTypes.Coordinator)
                {
                    if (connect.Password == settings.Password)
                    {
                        var coordinator = new Coordinator()
                        {
                            Id = player.id,
                            Name = connect.Name,
                            UserId = connect.UserId
                        };
                        AddCoordinator(coordinator);

                        //Give the newly connected coordinator their Self and State
                        SendToOverlayClient(player.id, new Packet(new ConnectResponse()
                        {
                            Type = ResponseType.Success,
                            Self = coordinator,
                            State = State,
                            Message = $"Connected to {settings.ServerName}!",
                            ServerVersion = VersionCode
                        }));
                    }
                    else
                    {
                        SendToOverlayClient(player.id, new Packet(new ConnectResponse()
                        {
                            Type = ResponseType.Fail,
                            State = State,
                            Message = $"Incorrect password for {settings.ServerName}!",
                            ServerVersion = VersionCode
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
                        _Score = x._Score,
                        FullCombo = x.FullCombo,
                        Color = x.Username == "Moon" ? "#00ff00" : "#ffffff"
                    });

                //If scores are disabled for this event, don't return them
                var @event = Database.Events.FirstOrDefault(x => x.EventId == request.EventId.ToString());
                if (((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.HideScoreFromPlayers))
                {
                    SendToOverlayClient(player.id, new Packet(new ScoreRequestResponse
                    {
                        Scores = new Score[] { }
                    }));
                }
                else
                {
                    SendToOverlayClient(player.id, new Packet(new ScoreRequestResponse
                    {
                        Scores = scores.ToArray()
                    }));
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

                    if ((scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? 0) < submitScore.Score._Score)
                    {
                        foreach (var score in scores) score.Old = true;

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
                            _Score = submitScore.Score._Score,
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
                            _Score = x._Score,
                            FullCombo = x.FullCombo,
                            Color = "#ffffff"
                        });

                    //Return the new scores for the song so the leaderboard will update immediately
                    //If scores are disabled for this event, don't return them
                    var @event = Database.Events.FirstOrDefault(x => x.EventId == submitScore.Score.EventId.ToString());
                    if (((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.HideScoreFromPlayers))
                    {
                        SendToOverlayClient(player.id, new Packet(new ScoreRequestResponse
                        {
                            Scores = new Score[] { }
                        }));
                    }
                    else
                    {
                        SendToOverlayClient(player.id, new Packet(new ScoreRequestResponse
                        {
                            Scores = newScores.ToArray()
                        }));
                    }
                }
            }
            else if (packet.Type == PacketType.Event)
            {
                Event @event = packet.SpecificPacket as Event;
                @event.ChangedObject = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(@event.ChangedObject).ToString());
                switch (@event.Type)
                {
                    case Event.EventType.CoordinatorAdded:
                        AddCoordinator(@event.ChangedObject as Coordinator);
                        break;
                    case Event.EventType.CoordinatorLeft:
                        @event.ChangedObject = JsonConvert.DeserializeObject<Coordinator>(@event.ChangedObject.ToString());
                        RemoveCoordinator(@event.ChangedObject as Coordinator);
                        break;
                    case Event.EventType.MatchCreated:
                        @event.ChangedObject = JsonConvert.DeserializeObject<Match>(@event.ChangedObject.ToString());
                        CreateMatch(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchUpdated:
                        @event.ChangedObject = JsonConvert.DeserializeObject<Match>(@event.ChangedObject.ToString());
                        UpdateMatch(@event.ChangedObject as Match);
                        break;
                    case Event.EventType.MatchDeleted:
                        @event.ChangedObject = JsonConvert.DeserializeObject<Match>(@event.ChangedObject.ToString());
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
                        @event.ChangedObject = JsonConvert.DeserializeObject<QualifierEvent>(@event.ChangedObject.ToString());
                        Send(player.id, new Packet(CreateQualifierEvent(@event.ChangedObject as QualifierEvent)));
                        break;
                    case Event.EventType.QualifierEventUpdated:
                        @event.ChangedObject = JsonConvert.DeserializeObject<QualifierEvent>(@event.ChangedObject.ToString());
                        Send(player.id, new Packet(UpdateQualifierEvent(@event.ChangedObject as QualifierEvent)));
                        break;
                    case Event.EventType.QualifierEventDeleted:
                        @event.ChangedObject = JsonConvert.DeserializeObject<QualifierEvent>(@event.ChangedObject.ToString());
                        Send(player.id, new Packet(DeleteQualifierEvent(@event.ChangedObject as QualifierEvent)));
                        break;
                    case Event.EventType.HostAdded:
                        AddHost(@event.ChangedObject as CoreServer);
                        break;
                    case Event.EventType.HostRemoved:
                        RemoveHost(@event.ChangedObject as CoreServer);
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
                var typeString = ((PacketType)forwardingPacket.Type).ToString();
                var packetType = Type.GetType($"TournamentAssistantShared.Models.Packets.{typeString}");
                forwardingPacket.SpecificPacket = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(forwardingPacket.SpecificPacket), packetType);
                var forwardedPacket = new Packet(forwardingPacket.SpecificPacket);

                //TODO: REMOVE
                /*var scoreboardClient = State.Coordinators.FirstOrDefault(x => x.Name == "[Scoreboard]");
                if (scoreboardClient != null) forwardingPacket.ForwardTo = forwardingPacket.ForwardTo.ToList().Union(new Guid[] { scoreboardClient.Id }).ToArray();*/

                ForwardTo(forwardingPacket.ForwardTo, packet.From, forwardedPacket);
            }
        }
    }
}
