using Microsoft.EntityFrameworkCore;
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
using TournamentAssistantCore.Sockets;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using TournamentAssistantShared.Utilities;
using static TournamentAssistantShared.Constants;
using static TournamentAssistantShared.Models.GameplayModifiers;
using static TournamentAssistantShared.Models.PlayerSpecificSettings;

namespace TournamentAssistantCore
{
    public class SystemServer : INotifyPropertyChanged
    {
        Server server;

        public event Func<Push.SongFinished, Task> PlayerFinishedSong;

        public event Func<Acknowledgement, Guid, Task> AckReceived;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //Tournament State can be modified by ANY client thread, so definitely needs thread-safe accessing
        private State _state;

        public State State
        {
            get { return _state; }
            set
            {
                _state = value;
                NotifyPropertyChanged(nameof(State));
            }
        }

        public User Self { get; set; }

        private StateManager StateManager { get; set; }

        public QualifierBot QualifierBot { get; private set; }
        public Discord.Database.QualifierDatabaseContext Database { get; private set; }

        //Reference to self as a server, if we are eligible for the Master Lists
        public CoreServer ServerSelf { get; private set; }

        //Server settings
        private Config config;
        private string address;
        private int port;
        private ServerSettings settings;
        private string botToken;

        //Update checker
        private CancellationTokenSource updateCheckToken = new();

        //Overlay settings
        private int overlayPort;

        public SystemServer(string botTokenArg = null)
        {
            config = new Config("serverConfig.json");

            var portValue = config.GetString("port");
            if (portValue == string.Empty)
            {
                portValue = "2052";
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
                overlayPortValue = "2053";
                config.SaveString("overlayPort", overlayPortValue);
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
                bannedModsValue = new string[]
                    {"IntroSkip", "AutoPauseStealth", "NoteSliceVisualizer", "SongChartVisualizer", "Custom Notes"};
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
                ScoreUpdateFrequency = Convert.ToInt32(scoreUpdateFrequencyValue),
            };
            settings.Teams.AddRange(teamsValue);
            settings.BannedMods.AddRange(bannedModsValue);

            address = addressValue;
            port = int.Parse(portValue);
            overlayPort = int.Parse(overlayPortValue);
            botToken = botTokenValue;
        }

        //Blocks until socket server begins to start (note that this is not "until server is started")
        public async void Start()
        {
            State = new State();
            State.ServerSettings = settings;
            State.KnownHosts.AddRange(config.GetHosts());

            Logger.Info($"Running on {Update.osType}");

            //Check for updates
            Logger.Info("Checking for updates...");
            bool gotRelease = false;

            try
            {
                var newVersion = await Update.GetLatestRelease();
                gotRelease = true;

                if (Version.Parse(VERSION) < newVersion)
                {
                    Logger.Error(
                        $"Update required! You are on \'{VERSION}\', new version is \'{newVersion}\'");
                    Logger.Info("Attempting AutoUpdate...");
                    bool UpdateSuccess = await Update.AttemptAutoUpdate();
                    if (!UpdateSuccess)
                    {
                        Logger.Error("AutoUpdate Failed. Please Update Manually. Shutting down");
                        SystemHost.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    }
                    else
                    {
                        Logger.Warning("Update was successful, exitting...");
                        SystemHost.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    }
                }
                else Logger.Success($"You are on the most recent version! ({VERSION})");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check for updates. Reason: " + ex.Message);
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

            StateManager = new StateManager(this);

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
                        Difficulty = x.BeatmapDifficulty,
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
            State.Events.AddRange(events
                .Select(x => Database.ConvertDatabaseToModel(getSongsForEvent(x.EventId).ToArray(), x)).ToArray());

            //Give our new server a sense of self :P
            Self = new User()
            {
                Guid = Guid.Empty.ToString(),
                Name = "HOST"
            };

            async Task scrapeServersAndStart(CoreServer core)
            {
                ServerSelf = core ?? new CoreServer
                {
                    Address = address == "[serverAddress]" ? "127.0.0.1" : address,
                    Port = port,
                    Name = settings.ServerName
                };

                //Wipe locally saved hosts - clean slate
                config.SaveHosts(new CoreServer[] { });

                //Scrape hosts. Unreachable hosts will be removed
                Logger.Info("Reaching out to other hosts for updated Master Lists...");

                //Commented out is the code that makes this act as a mesh network
                //var hostStatePairs = await HostScraper.ScrapeHosts(State.KnownHosts, settings.ServerName, 0, core);

                //The uncommented duplicate here makes this act as a hub and spoke network, since MasterServer is the domain of the master server
                var hostStatePairs = await HostScraper.ScrapeHosts(
                    State.KnownHosts.Where(x => x.Address.Contains(MASTER_SERVER)).ToArray(),
                    settings.ServerName,
                    0,
                    core);

                hostStatePairs = hostStatePairs.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value);
                var newHostList = hostStatePairs.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts).Union(hostStatePairs.Keys, new CoreServerEqualityComparer());
                State.KnownHosts.Clear();
                State.KnownHosts.AddRange(newHostList.ToArray());

                //The current server will always remove itself from its list thanks to it not being up when
                //it starts. Let's fix that. Also, add back the Master Server if it was removed.
                //We accomplish this by triggering the default-on-empty function of GetHosts()
                if (State.KnownHosts.Count == 0) State.KnownHosts.AddRange(config.GetHosts());
                if (core != null)
                {
                    var oldHosts = State.KnownHosts.ToArray();
                    State.KnownHosts.Clear();
                    State.KnownHosts.AddRange(oldHosts.Union(new CoreServer[] { core }, new CoreServerEqualityComparer()).ToArray());
                }

                config.SaveHosts(State.KnownHosts.ToArray());
                Logger.Info("Server list updated.");

                await OpenPort(port);
                await OpenPort(overlayPort);

                server = new Server(port, overlayPort);
                server.PacketReceived += Server_PacketReceived;
                server.ClientConnected += Server_ClientConnected;
                server.ClientDisconnected += Server_ClientDisconnected;
                server.Start();


                if (gotRelease)
                {
                    //Start a regular check for updates
                    Update.PollForUpdates(() =>
                    {
                        server.Shutdown();
                        SystemHost.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    }, updateCheckToken.Token);
                }
            }

            //Verify that the provided address points to our server
            if (IPAddress.TryParse(address, out _))
            {
                Logger.Warning(
                    $"\'{address}\' seems to be an IP address. You'll need a domain pointed to your server for it to be added to the Master Lists");
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
                    if (packet.packetCase == Packet.packetOneofCase.Request && packet.Request.TypeCase == Request.TypeOneofCase.connect)
                    {
                        var connect = packet.Request.connect;
                        if (connect.User.Name == keyName)
                        {
                            verified = true;
                            connected.Set();
                        }
                    }

                    return Task.CompletedTask;
                };

                verificationServer.Start();

                var client = new TemporaryClient(address, port, keyName, "0", User.ClientTypes.TemporaryConnection);
                await client.Start();

                connected.WaitOne(6000);

                client.Shutdown();
                verificationServer.Shutdown();

                if (verified)
                {
                    Logger.Success(
                        "Verified address! Server should be added to the Lists of all servers that were scraped for hosts");

                    await scrapeServersAndStart(new CoreServer
                    {
                        Address = address,
                        Port = port,
                        Name = State.ServerSettings.ServerName
                    });
                }
                else
                {
                    Logger.Warning(
                        "Failed to verify address. Continuing server startup, but note that this server was not added to the Master Lists, if it wasn't already there");
                    await scrapeServersAndStart(null);
                }
            }
            else
            {
                Logger.Warning(
                    "If you provide a value for \'serverAddress\' in the configuration file, your server can be added to the Master Lists");
                await scrapeServersAndStart(null);
            }
        }

        //Courtesy of andruzzzhka's Multiplayer
        async Task OpenPort(int port)
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
                Logger.Warning(
                    $"Can't open port {port} using UPnP! (This is only relevant for people behind NAT who don't port forward. If you're being hosted by an actual server, or you've set up port forwarding manually, you can safely ignore this message. As well as any other yellow messages... Yellow means \"warning\" folks.");
            }
        }

        private async Task Server_ClientDisconnected(ConnectedUser client)
        {
            Logger.Debug("Client Disconnected!");

            var user = StateManager.GetUserById(client.id.ToString());
            if (user != null)
            {
                await StateManager.RemoveUser(user).ConfigureAwait(false);
            }
        }

        private Task Server_ClientConnected(ConnectedUser client)
        {
            return Task.CompletedTask;
        }

        static string LogPacket(Packet packet)
        {
            string secondaryInfo = string.Empty;
            if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.play_song)
                {
                    var playSong = command.play_song;
                    secondaryInfo = playSong.GameplayParameters.Beatmap.LevelId + " : " +
                                    playSong.GameplayParameters.Beatmap.Difficulty;
                }
                else if (command.TypeCase == Command.TypeOneofCase.load_song)
                {
                    var loadSong = command.load_song;
                    secondaryInfo = loadSong.LevelId;
                }
                else
                {
                    secondaryInfo = command.TypeCase.ToString();
                }
            }

            if (packet.packetCase == Packet.packetOneofCase.Event)
            {
                var @event = packet.Event;

                secondaryInfo = @event.ChangedObjectCase.ToString();
                if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.user_updated_event)
                {
                    var user = @event.user_updated_event.User;
                    secondaryInfo =
                        $"{secondaryInfo} from ({user.Name} : {user.DownloadState}) : ({user.PlayState} : {user.StreamDelayMs})";
                }
                else if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.match_updated_event)
                {
                    var match = @event.match_updated_event.Match;
                    secondaryInfo = $"{secondaryInfo} ({match.SelectedDifficulty})";
                }
            }
            if (packet.packetCase == Packet.packetOneofCase.ForwardingPacket)
            {
                var forwardedpacketCase = packet.ForwardingPacket.Packet.packetCase;
                secondaryInfo = $"{forwardedpacketCase}";
            }

            return $"({packet.packetCase}) ({secondaryInfo})";
        }

        public async Task Send(Guid id, Packet packet)
        {
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            packet.From = Self?.Guid ?? Guid.Empty.ToString();
            await server.Send(id, new PacketWrapper(packet));
        }

        public async Task Send(Guid[] ids, Packet packet)
        {
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            packet.From = Self?.Guid ?? Guid.Empty.ToString();
            await server.Send(ids, new PacketWrapper(packet));
        }

        public async Task ForwardTo(Guid[] ids, Guid from, Packet packet)
        {
            packet.From = from.ToString();
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            await server.Send(ids, new PacketWrapper(packet));
        }

        public async Task BroadcastToAllClients(Packet packet)
        {
            packet.From = Self.Guid;
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            await server.Broadcast(new PacketWrapper(packet));
        }

        #region EventManagement
        public async Task<Response> SendCreateQualifierEvent(CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host.CoreServerEquals(ServerSelf))
            {
                return await CreateQualifierEvent(qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet
                {
                    Event = new Event
                    {
                        qualifier_created_event = new Event.QualifierCreatedEvent
                        {
                            Event = qualifierEvent
                        }
                    }
                }, $"{ServerSelf.Address}:{ServerSelf.Port}", 0);
                return result?.Response ?? new Response
                {
                    Type = Response.ResponseType.Fail,
                    modify_qualifier = new Response.ModifyQualifier
                    {
                        Message = "The request to the designated server timed out. The server is offline or otherwise unreachable"
                    }
                };
            }
        }

        public async Task<Response> SendUpdateQualifierEvent(CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host.CoreServerEquals(ServerSelf))
            {
                return await UpdateQualifierEvent(qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet
                {
                    Event = new Event
                    {
                        qualifier_updated_event = new Event.QualifierUpdatedEvent
                        {
                            Event = qualifierEvent
                        }
                    }
                }, $"{ServerSelf.Address}:{ServerSelf.Port}", 0);
                return result?.Response ?? new Response
                {
                    Type = Response.ResponseType.Fail,
                    modify_qualifier = new Response.ModifyQualifier
                    {
                        Message = "The request to the designated server timed out. The server is offline or otherwise unreachable"
                    }
                };
            }
        }

        public async Task<Response> SendDeleteQualifierEvent(CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host.CoreServerEquals(ServerSelf))
            {
                return await DeleteQualifierEvent(qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet
                {
                    Event = new Event
                    {
                        qualifier_deleted_event = new Event.QualifierDeletedEvent
                        {
                            Event = qualifierEvent
                        }
                    }
                }, $"{ServerSelf.Address}:{ServerSelf.Port}", 0);
                return result?.Response ?? new Response
                {
                    Type = Response.ResponseType.Fail,
                    modify_qualifier = new Response.ModifyQualifier
                    {
                        Message = "The request to the designated server timed out. The server is offline or otherwise unreachable"
                    }
                };
            }
        }

        public async Task<Response> CreateQualifierEvent(QualifierEvent qualifierEvent)
        {
            //No more limiting number of events per guild
            /*if (Database.Events.Any(x => !x.Old && x.GuildId == (ulong)qualifierEvent.Guild.Id))
            {
                return new Response
                {
                    Type = Response.ResponseType.Fail,
                    modify_qualifier = new Response.ModifyQualifier
                    {
                        Message = "There is already an event running for your guild"
                    }
                };
            }*/

            var databaseEvent = Database.ConvertModelToEventDatabase(qualifierEvent);
            Database.Events.Add(databaseEvent);
            await Database.SaveChangesAsync();

            lock (State)
            {
                State.Events.Add(qualifierEvent);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                qualifier_created_event = new Event.QualifierCreatedEvent
                {
                    Event = qualifierEvent
                }
            };
            await BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            return new Response
            {
                Type = Response.ResponseType.Success,
                modify_qualifier = new Response.ModifyQualifier
                {
                    Message = $"Successfully created event: {databaseEvent.Name} with settings: {(QualifierEvent.EventSettings)databaseEvent.Flags}"
                }
            };
        }

        public async Task<Response> UpdateQualifierEvent(QualifierEvent qualifierEvent)
        {
            //No more limiting number of events per guild
            /*if (!Database.Events.Any(x => !x.Old && x.GuildId == (ulong)qualifierEvent.Guild.Id))
            {
                return new Response
                {
                    Type = Response.ResponseType.Fail,
                    modify_qualifier = new Response.ModifyQualifier
                    {
                        Message = "There is not an event running for your guild"
                    }
                };
            }*/

            //Update Event entry
            var newDatabaseEvent = Database.ConvertModelToEventDatabase(qualifierEvent);
            Database.Entry(Database.Events.First(x => x.EventId == qualifierEvent.Guid.ToString())).CurrentValues
                .SetValues(newDatabaseEvent);

            //Check for removed songs
            foreach (var song in Database.Songs.Where(x => x.EventId == qualifierEvent.Guid.ToString() && !x.Old))
            {
                if (!qualifierEvent.QualifierMaps.Any(x => song.LevelId == x.Beatmap.LevelId &&
                                                           song.Characteristic ==
                                                           x.Beatmap.Characteristic.SerializedName &&
                                                           song.BeatmapDifficulty == x.Beatmap.Difficulty &&
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
                                             x.EventId == qualifierEvent.Guid.ToString() &&
                                             x.LevelId == song.Beatmap.LevelId &&
                                             x.Characteristic == song.Beatmap.Characteristic.SerializedName &&
                                             x.BeatmapDifficulty == song.Beatmap.Difficulty &&
                                             x.GameOptions == (int)song.GameplayModifiers.Options &&
                                             x.PlayerOptions == (int)song.PlayerSettings.Options))
                {
                    Database.Songs.Add(new Discord.Database.Song
                    {
                        EventId = qualifierEvent.Guid.ToString(),
                        LevelId = song.Beatmap.LevelId,
                        Name = song.Beatmap.Name,
                        Characteristic = song.Beatmap.Characteristic.SerializedName,
                        BeatmapDifficulty = song.Beatmap.Difficulty,
                        GameOptions = (int)song.GameplayModifiers.Options,
                        PlayerOptions = (int)song.PlayerSettings.Options
                    });
                }
            }

            await Database.SaveChangesAsync();

            lock (State)
            {
                var eventToReplace = State.Events.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
                State.Events.Remove(eventToReplace);
                State.Events.Add(qualifierEvent);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                qualifier_updated_event = new Event.QualifierUpdatedEvent
                {
                    Event = qualifierEvent
                }
            };

            var updatePacket = new Packet
            {
                Event = @event
            };

            await BroadcastToAllClients(updatePacket);

            return new Response
            {
                Type = Response.ResponseType.Success,
                modify_qualifier = new Response.ModifyQualifier
                {
                    Message = $"Successfully updated event: {newDatabaseEvent.Name}"
                }
            };
        }

        public async Task<Response> DeleteQualifierEvent(QualifierEvent qualifierEvent)
        {
            //No more limiting number of events per guild
            /*if (!Database.Events.Any(x => !x.Old && x.GuildId == (ulong)qualifierEvent.Guild.Id))
            {
                return new Response
                {
                    Type = Response.ResponseType.Fail,
                    modify_qualifier = new Response.ModifyQualifier
                    {
                        Message = "There is not an event running for your guild"
                    }
                };
            }*/

            //Mark all songs and scores as old
            await Database.Events.Where(x => x.EventId == qualifierEvent.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Database.Songs.Where(x => x.EventId == qualifierEvent.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Database.Scores.Where(x => x.EventId == qualifierEvent.Guid.ToString()).ForEachAsync(x => x.Old = true);
            await Database.SaveChangesAsync();

            lock (State)
            {
                var eventToRemove = State.Events.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
                State.Events.Remove(eventToRemove);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                qualifier_deleted_event = new Event.QualifierDeletedEvent
                {
                    Event = qualifierEvent
                }
            };
            await BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            return new Response
            {
                Type = Response.ResponseType.Success,
                modify_qualifier = new Response.ModifyQualifier
                {
                    Message = $"Successfully ended event: {qualifierEvent.Name}"
                }
            };
        }

        public async Task AddHost(CoreServer host)
        {
            lock (State)
            {
                var oldHosts = State.KnownHosts.ToArray();
                State.KnownHosts.Clear();
                State.KnownHosts.AddRange(oldHosts.Union(new[] { host }, new CoreServerEqualityComparer()));

                //Save to disk
                config.SaveHosts(State.KnownHosts.ToArray());
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                host_added_event = new Event.HostAddedEvent
                {
                    Server = host
                }
            };
            await BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        public async Task RemoveHost(CoreServer host)
        {
            lock (State)
            {
                var hostToRemove = State.KnownHosts.FirstOrDefault(x => x.CoreServerEquals(host));
                State.KnownHosts.Remove(hostToRemove);
            }

            NotifyPropertyChanged(nameof(State));

            var @event = new Event
            {
                host_deleted_event = new Event.HostDeletedEvent
                {
                    Server = host
                }
            };
            await BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        #endregion EventManagement

        private async Task Server_PacketReceived(ConnectedUser user, Packet packet)
        {
            Logger.Debug($"Received data: {LogPacket(packet)}");

            //Ready to go, only disabled since it is currently unusued
            /*if (packet.Type != PacketType.Acknowledgement)
            {
                Send(packet.From, new Packet(new Acknowledgement()
                {
                    PacketId = packet.Id
                }));
            }*/

            if (packet.packetCase == Packet.packetOneofCase.Acknowledgement)
            {
                Acknowledgement acknowledgement = packet.Acknowledgement;
                AckReceived?.Invoke(acknowledgement, Guid.Parse(packet.From));
            }
            else if (packet.packetCase == Packet.packetOneofCase.Command)
            {
                var command = packet.Command;
                if (command.TypeCase == Command.TypeOneofCase.send_bot_message)
                {
                    var sendBotMessage = command.send_bot_message;
                    QualifierBot.SendMessage(sendBotMessage.Channel, sendBotMessage.Message);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Push)
            {
                var push = packet.Push;
                if (push.DataCase == Push.DataOneofCase.LeaderboardScore)
                {
                    var qualifierScore = push.LeaderboardScore;

                    //Check to see if the song exists in the database
                    var song = Database.Songs.FirstOrDefault(x => x.EventId == qualifierScore.EventId.ToString() &&
                                                                  x.LevelId == qualifierScore.Parameters.Beatmap
                                                                      .LevelId &&
                                                                  x.Characteristic == qualifierScore.Parameters.Beatmap
                                                                      .Characteristic.SerializedName &&
                                                                  x.BeatmapDifficulty == qualifierScore.Parameters
                                                                      .Beatmap.Difficulty &&
                                                                  x.GameOptions == (int)qualifierScore.Parameters
                                                                      .GameplayModifiers.Options &&
                                                                  //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                                                                  !x.Old);

                    if (song != null)
                    {
                        //Mark all older scores as old
                        var scores = Database.Scores
                            .Where(x => x.EventId == qualifierScore.EventId.ToString() &&
                                        x.LevelId == qualifierScore.Parameters.Beatmap.LevelId &&
                                        x.Characteristic == qualifierScore.Parameters.Beatmap.Characteristic
                                            .SerializedName &&
                                        x.BeatmapDifficulty == qualifierScore.Parameters.Beatmap.Difficulty &&
                                        x.GameOptions == (int)qualifierScore.Parameters.GameplayModifiers.Options &&
                                        //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                                        !x.Old &&
                                        x.UserId == ulong.Parse(qualifierScore.UserId));

                        var oldHighScore = (scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? -1);
                        if (oldHighScore < qualifierScore.Score)
                        {
                            foreach (var score in scores) score.Old = true;

                            Database.Scores.Add(new Discord.Database.Score
                            {
                                EventId = qualifierScore.EventId.ToString(),
                                UserId = ulong.Parse(qualifierScore.UserId),
                                Username = qualifierScore.Username,
                                LevelId = qualifierScore.Parameters.Beatmap.LevelId,
                                Characteristic = qualifierScore.Parameters.Beatmap.Characteristic.SerializedName,
                                BeatmapDifficulty = qualifierScore.Parameters.Beatmap.Difficulty,
                                GameOptions = (int)qualifierScore.Parameters.GameplayModifiers.Options,
                                PlayerOptions = (int)qualifierScore.Parameters.PlayerSettings.Options,
                                _Score = qualifierScore.Score,
                                FullCombo = qualifierScore.FullCombo,
                            });
                            await Database.SaveChangesAsync();
                        }

                        var newScores = Database.Scores
                            .Where(x => x.EventId == qualifierScore.EventId.ToString() &&
                                        x.LevelId == qualifierScore.Parameters.Beatmap.LevelId &&
                                        x.Characteristic == qualifierScore.Parameters.Beatmap.Characteristic
                                            .SerializedName &&
                                        x.BeatmapDifficulty == qualifierScore.Parameters.Beatmap.Difficulty &&
                                        x.GameOptions == (int)qualifierScore.Parameters.GameplayModifiers.Options &&
                                        //x.PlayerOptions == (int)submitScore.Score.Parameters.PlayerSettings.Options &&
                                        !x.Old).OrderByDescending(x => x._Score).Take(10)
                            .Select(x => new LeaderboardScore
                            {
                                EventId = qualifierScore.EventId,
                                Parameters = qualifierScore.Parameters,
                                Username = x.Username,
                                UserId = x.UserId.ToString(),
                                Score = x._Score,
                                FullCombo = x.FullCombo,
                                Color = "#ffffff"
                            });

                        //Return the new scores for the song so the leaderboard will update immediately
                        //If scores are disabled for this event, don't return them
                        var @event = Database.Events.FirstOrDefault(x => x.EventId == qualifierScore.EventId.ToString());
                        var hideScores =
                            ((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings
                                .HideScoresFromPlayers);
                        var enableLeaderboardMessage =
                            ((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings
                                .EnableLeaderboardMessage);

                        var scoreRequestResponse = new Response.LeaderboardScores();
                        scoreRequestResponse.Scores.AddRange(hideScores ? new LeaderboardScore[] { } : newScores.ToArray());

                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                leaderboard_scores = scoreRequestResponse,
                                RespondingToPacketId = packet.Id
                            }
                        });

                        if (oldHighScore < qualifierScore.Score && @event.InfoChannelId != default && !hideScores && QualifierBot != null)
                        {
                            QualifierBot.SendScoreEvent(@event.InfoChannelId, qualifierScore);

                            if (enableLeaderboardMessage)
                            {
                                var eventSongs = Database.Songs.Where(x => x.EventId == qualifierScore.EventId.ToString() && !x.Old);
                                var eventScores = Database.Scores.Where(x => x.EventId == qualifierScore.EventId.ToString() && !x.Old);
                                var newMessageId = await QualifierBot.SendLeaderboardUpdate(@event.InfoChannelId, @event.LeaderboardMessageId, eventScores.ToList(), eventSongs.ToList());
                                if (@event.LeaderboardMessageId != newMessageId)
                                {
                                    @event.LeaderboardMessageId = newMessageId;
                                    await Database.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
                else if (push.DataCase == Push.DataOneofCase.song_finished)
                {
                    var finalScore = push.song_finished;

                    await BroadcastToAllClients(packet); //TODO: Should be targeted
                    PlayerFinishedSong?.Invoke(finalScore);
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Request)
            {
                var request = packet.Request;
                if (request.TypeCase == Request.TypeOneofCase.connect)
                {
                    var connect = request.connect;
                    if (connect.ClientVersion != VERSION_CODE)
                    {
                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                Type = Response.ResponseType.Fail,
                                connect = new Response.Connect
                                {
                                    ServerVersion = VERSION_CODE,
                                    Message = $"Version mismatch, this server is on version {VERSION}",
                                    Reason = Response.ConnectFailReason.IncorrectVersion
                                },
                                RespondingToPacketId = packet.Id
                            }
                        });
                    }
                    else if (string.IsNullOrWhiteSpace(settings.Password) || connect.Password == settings.Password)
                    {
                        connect.User.Guid = user.id.ToString();
                        await StateManager.AddUser(connect.User);

                        //Give the newly connected player their Self and State
                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                Type = Response.ResponseType.Success,
                                connect = new Response.Connect
                                {
                                    SelfGuid = user.id.ToString(),
                                    State = State,
                                    ServerVersion = VERSION_CODE,
                                    Message = $"Connected to {settings.ServerName}!"
                                },
                                RespondingToPacketId = packet.Id
                            }
                        });
                    }
                    else
                    {
                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                Type = Response.ResponseType.Fail,
                                connect = new Response.Connect
                                {
                                    ServerVersion = VERSION_CODE,
                                    Message = $"Incorrect password for {settings.ServerName}!",
                                    Reason = Response.ConnectFailReason.IncorrectPassword
                                },
                                RespondingToPacketId = packet.Id
                            }
                        });
                    }
                }
                else if (request.TypeCase == Request.TypeOneofCase.leaderboard_score)
                {
                    var scoreRequest = request.leaderboard_score;
                    var scores = Database.Scores
                    .Where(x => x.EventId == scoreRequest.EventId.ToString() &&
                                x.LevelId == scoreRequest.Parameters.Beatmap.LevelId &&
                                x.Characteristic == scoreRequest.Parameters.Beatmap.Characteristic.SerializedName &&
                                x.BeatmapDifficulty == scoreRequest.Parameters.Beatmap.Difficulty &&
                                x.GameOptions == (int)scoreRequest.Parameters.GameplayModifiers.Options &&
                                //x.PlayerOptions == (int)scoreRequest.Parameters.PlayerSettings.Options &&
                                !x.Old).OrderByDescending(x => x._Score)
                    .Select(x => new LeaderboardScore
                    {
                        EventId = scoreRequest.EventId,
                        Parameters = scoreRequest.Parameters,
                        Username = x.Username,
                        UserId = x.UserId.ToString(),
                        Score = x._Score,
                        FullCombo = x.FullCombo,
                        Color = x.Username == "Moon" ? "#00ff00" : "#ffffff"
                    });

                    //If scores are disabled for this event, don't return them
                    var @event = Database.Events.FirstOrDefault(x => x.EventId == scoreRequest.EventId.ToString());
                    if (((QualifierEvent.EventSettings)@event.Flags).HasFlag(QualifierEvent.EventSettings.HideScoresFromPlayers))
                    {
                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                leaderboard_scores = new Response.LeaderboardScores(),
                                RespondingToPacketId = packet.Id
                            }
                        });
                    }
                    else
                    {
                        var scoreRequestResponse = new Response.LeaderboardScores();
                        scoreRequestResponse.Scores.AddRange(scores);

                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                leaderboard_scores = scoreRequestResponse,
                                RespondingToPacketId = packet.Id
                            }
                        });
                    }
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Response)
            {
                var response = packet.Response;
                if (response.DetailsCase == Response.DetailsOneofCase.modal)
                {
                    await BroadcastToAllClients(packet); //TODO: Should be targeted
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.ForwardingPacket)
            {
                var forwardingPacket = packet.ForwardingPacket;
                var forwardedPacket = forwardingPacket.Packet;

                await ForwardTo(forwardingPacket.ForwardToes.Select(x => Guid.Parse(x)).ToArray(),
                    Guid.Parse(packet.From),
                    forwardedPacket);
            }
            else if (packet.packetCase == Packet.packetOneofCase.Event)
            {
                Event @event = packet.Event;
                switch (@event.ChangedObjectCase)
                {
                    case Event.ChangedObjectOneofCase.match_created_event:
                        await StateManager.CreateMatch(@event.match_created_event.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_updated_event:
                        await StateManager.UpdateMatch(@event.match_updated_event.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_deleted_event:
                        await StateManager.DeleteMatch(@event.match_deleted_event.Match);
                        break;
                    case Event.ChangedObjectOneofCase.user_added_event:
                        await StateManager.AddUser(@event.user_added_event.User);
                        break;
                    case Event.ChangedObjectOneofCase.user_updated_event:
                        await StateManager.UpdateUser(@event.user_updated_event.User);
                        break;
                    case Event.ChangedObjectOneofCase.user_left_event:
                        await StateManager.RemoveUser(@event.user_left_event.User);
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_created_event:
                        var createResponse = await CreateQualifierEvent(@event.qualifier_created_event.Event);
                        createResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = createResponse,
                        });
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_updated_event:
                        var updateResponse = await UpdateQualifierEvent(@event.qualifier_updated_event.Event);
                        updateResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = updateResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_deleted_event:
                        var deleteResponse = await DeleteQualifierEvent(@event.qualifier_deleted_event.Event);
                        deleteResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = deleteResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.host_added_event:
                        await AddHost(@event.host_added_event.Server);
                        break;
                    case Event.ChangedObjectOneofCase.host_deleted_event:
                        await RemoveHost(@event.host_deleted_event.Server);
                        break;
                    default:
                        Logger.Error($"Unknown command received from {user.id}!");
                        break;
                }
            }
        }
    }
}