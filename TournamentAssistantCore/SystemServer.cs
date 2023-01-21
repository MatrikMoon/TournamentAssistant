using Microsoft.EntityFrameworkCore;
using Open.Nat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantCore.Database.Contexts;
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

namespace TournamentAssistantCore
{
    public class SystemServer
    {
        Server server;

        public event Func<User, Task> UserConnected;
        public event Func<User, Task> UserDisconnected;
        public event Func<User, Task> UserInfoUpdated;
        public event Func<Match, Task> MatchInfoUpdated;
        public event Func<Match, Task> MatchCreated;
        public event Func<Match, Task> MatchDeleted;

        public event Func<Push.SongFinished, Task> PlayerFinishedSong;

        public event Func<Acknowledgement, Guid, Task> AckReceived;

        //Tournament State can be modified by ANY client thread, so definitely needs thread-safe accessing
        private State State { get; set; }

        public User Self { get; set; }

        public QualifierBot QualifierBot { get; private set; }
        public QualifierDatabaseContext QualifierDatabase { get; private set; }

        //Reference to self as a server, if we are eligible for the Master Lists
        public CoreServer ServerSelf { get; private set; }

        //Server settings
        private Config config;
        private string address;
        private int port;
        private string botToken;
        private string serverName;

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

            var addressValue = config.GetString("serverAddress");
            if (addressValue == string.Empty || addressValue == "[serverAddress]")
            {
                addressValue = "[serverAddress]";
                config.SaveString("serverAddress", addressValue);
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

            address = addressValue;
            port = int.Parse(portValue);
            overlayPort = int.Parse(overlayPortValue);
            botToken = botTokenValue;
            serverName = nameValue;
        }

        public List<Tournament> GetTournaments()
        {
            lock (State.Tournaments)
            {
                return State.Tournaments.ToList();
            }
        }

        public Tournament GetTournamentByGuid(string guid)
        {
            lock (State.Tournaments)
            {
                return State.Tournaments.FirstOrDefault(x => x.Guid == guid);
            }
        }

        public List<User> GetUsers(string tournamentGuid)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            lock (tournament.Users)
            {
                return tournament.Users.ToList();
            }
        }

        public User GetUserById(string tournamentGuid, string guid)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            lock (tournament.Users)
            {
                return tournament.Users.FirstOrDefault(x => x.Guid == guid.ToString());
            }
        }

        public List<Match> GetMatches(string tournamentGuid)
        {
            var tournament = GetTournamentByGuid(tournamentGuid);
            lock (tournament.Matches)
            {
                return tournament.Matches.ToList();
            }
        }

        public List<CoreServer> GetServers()
        {
            lock (State.KnownServers)
            {
                return State.KnownServers.ToList();
            }
        }

        //Blocks until socket server begins to start (note that this is not "until server is started")
        public async void Start()
        {
            State = new State();
            State.KnownServers.AddRange(config.GetServers());

            Logger.Info($"Running on {Update.osType}");

            //Check for updates
            Logger.Info("Checking for updates...");
            var newVersion = await Update.GetLatestRelease();
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
                QualifierDatabase = QualifierBot.Database;
            }
            else
            {
                //If the bot's not running, we need to start the service manually
                var service = new DatabaseService();
                QualifierDatabase = service.QualifierDatabaseContext;
            }

            //Translate Event and Songs from database to model format
            //Don't need to lock this since it happens on startup
            foreach (var tournament in State.Tournaments)
            {
                tournament.Qualifiers.AddRange(await QualifierDatabase.LoadModelsFromDatabase(tournament));
            }

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
                    Name = serverName
                };

                //Wipe locally saved hosts - clean slate
                config.SaveServers(new CoreServer[] { });

                //Scrape hosts. Unreachable hosts will be removed
                Logger.Info("Reaching out to other hosts for updated Master Lists...");

                //Commented out is the code that makes this act as a mesh network
                //var hostStatePairs = await HostScraper.ScrapeHosts(State.KnownServers, settings.ServerName, 0, core);

                //The uncommented duplicate here makes this act as a hub and spoke network, since MasterServer is the domain of the master server
                var hostStatePairs = await HostScraper.ScrapeHosts(
                    State.KnownServers.Where(x => x.Address.Contains(MASTER_SERVER)).ToArray(),
                    serverName,
                    0,
                    core);

                hostStatePairs = hostStatePairs.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value);
                var newHostList = hostStatePairs.Values.Where(x => x.KnownServers != null).SelectMany(x => x.KnownServers).Union(hostStatePairs.Keys, new CoreServerEqualityComparer());
                State.KnownServers.Clear();
                State.KnownServers.AddRange(newHostList.ToArray());

                //The current server will always remove itself from its list thanks to it not being up when
                //it starts. Let's fix that. Also, add back the Master Server if it was removed.
                //We accomplish this by triggering the default-on-empty function of GetServers()
                if (State.KnownServers.Count == 0) State.KnownServers.AddRange(config.GetServers());
                if (core != null)
                {
                    var oldHosts = State.KnownServers.ToArray();
                    State.KnownServers.Clear();
                    State.KnownServers.AddRange(oldHosts.Union(new CoreServer[] { core }, new CoreServerEqualityComparer()).ToArray());
                }

                config.SaveServers(State.KnownServers.ToArray());
                Logger.Info("Server list updated.");

                await OpenPort(port);
                await OpenPort(overlayPort);

                server = new Server(port, overlayPort);
                server.PacketReceived += Server_PacketReceived;
                server.ClientConnected += Server_ClientConnected;
                server.ClientDisconnected += Server_ClientDisconnected;
                server.Start();

                //Start a regular check for updates
                Update.PollForUpdates(() =>
                {
                    server.Shutdown();
                    Environment.Exit(0);
                }, updateCheckToken.Token);
            }

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
                    Logger.Success("Verified address! Server should be added to the Lists of all servers that were scraped for hosts");

                    await scrapeServersAndStart(new CoreServer
                    {
                        Address = address,
                        Port = port,
                        Name = serverName
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
                Logger.Warning($"Can't open port {port} using UPnP! (This is only relevant for people behind NAT who don't port forward. If you're being hosted by an actual server, or you've set up port forwarding manually, you can safely ignore this message. As well as any other yellow messages... Yellow means \"warning\" folks.");
            }
        }

        private async Task Server_ClientDisconnected(ConnectedUser client)
        {
            Logger.Debug("Client Disconnected!");

            Tournament targetTournament = null;
            foreach (var tournament in GetTournaments())
            {
                var users = GetUsers(tournament.Guid);
                if (users.Any(x => x.Guid == client.id.ToString()))
                {
                    targetTournament = tournament;
                    break;
                }
            }

            if (targetTournament != null)
            {
                var user = GetUsers(targetTournament.Guid).First(x => x.Guid == client.id.ToString());
                await RemoveUser(targetTournament.Guid, user);
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

        private async Task BroadcastToAllInTournament(Guid tournamentId, Packet packet)
        {
            packet.From = Self.Guid;
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            await server.Send(GetUsers(tournamentId.ToString()).Select(x => Guid.Parse(x.Guid)).ToArray(), new PacketWrapper(packet));
        }

        private async Task BroadcastToAllClients(Packet packet)
        {
            packet.From = Self.Guid;
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            await server.Broadcast(new PacketWrapper(packet));
        }

        #region EventManagement

        public async Task AddUser(string tournamentId, User user)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Users)
            {
                tournament.Users.Add(user);
            }

            var @event = new Event
            {
                user_added_event = new Event.UserAddedEvent
                {
                    TournamentGuid = tournamentId,
                    User = user
                }
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (UserConnected != null) await UserConnected.Invoke(user);
        }

        public async Task UpdateUser(string tournamentId, User user)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Users)
            {
                var userToReplace = tournament.Users.FirstOrDefault(x => x.UserEquals(user));
                tournament.Users.Remove(userToReplace);
                tournament.Users.Add(user);
            }

            var @event = new Event
            {
                user_updated_event = new Event.UserUpdatedEvent
                {
                    TournamentGuid = tournamentId,
                    User = user
                }
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (UserInfoUpdated != null) await UserInfoUpdated.Invoke(user);
        }

        public async Task RemoveUser(string tournamentId, User user)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Users)
            {
                tournament.Users.RemoveAll(x => x.Guid == user.Guid);
            }

            var @event = new Event
            {
                user_left_event = new Event.UserLeftEvent
                {
                    TournamentGuid = tournamentId,
                    User = user
                }
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            //Remove disconnected user from any matches they're in
            foreach (var match in GetMatches(tournamentId))
            {
                if (match.AssociatedUsers.Contains(user.Guid))
                {
                    match.AssociatedUsers.RemoveAll(x => x == user.Guid);
                    await UpdateMatch(tournamentId, match);
                }
            }

            if (UserDisconnected != null) await UserDisconnected.Invoke(user);
        }

        public async Task CreateMatch(string tournamentId, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Matches)
            {
                tournament.Matches.Add(match);
            }

            var @event = new Event
            {
                match_created_event = new Event.MatchCreatedEvent
                {
                    TournamentGuid = tournamentId,
                    Match = match
                }
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (MatchCreated != null) await MatchCreated.Invoke(match);
        }

        public async Task UpdateMatch(string tournamentId, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Matches)
            {
                var matchToReplace = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
                tournament.Matches.Remove(matchToReplace);
                tournament.Matches.Add(match);
            }

            var @event = new Event
            {
                match_updated_event = new Event.MatchUpdatedEvent
                {
                    TournamentGuid = tournamentId,
                    Match = match
                }
            };

            var updatePacket = new Packet
            {
                Event = @event
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), updatePacket);

            if (MatchInfoUpdated != null) await MatchInfoUpdated.Invoke(match);
        }

        public async Task DeleteMatch(string tournamentId, Match match)
        {
            var tournament = GetTournamentByGuid(tournamentId);
            lock (tournament.Matches)
            {
                var matchToRemove = tournament.Matches.FirstOrDefault(x => x.MatchEquals(match));
                tournament.Matches.Remove(matchToRemove);
            }

            var @event = new Event
            {
                match_deleted_event = new Event.MatchDeletedEvent
                {
                    TournamentGuid = tournamentId,
                    Match = match
                }
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            if (MatchDeleted != null) await MatchDeleted.Invoke(match);
        }

        public async Task<Response> SendCreateQualifierEvent(string tournamentId, CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host.CoreServerEquals(ServerSelf))
            {
                return await CreateQualifierEvent(tournamentId, qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet
                {
                    Event = new Event
                    {
                        qualifier_created_event = new Event.QualifierCreatedEvent
                        {
                            TournamentGuid = tournamentId,
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

        public async Task<Response> SendUpdateQualifierEvent(string tournamentId, CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host.CoreServerEquals(ServerSelf))
            {
                return await UpdateQualifierEvent(tournamentId, qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet
                {
                    Event = new Event
                    {
                        qualifier_updated_event = new Event.QualifierUpdatedEvent
                        {
                            TournamentGuid = tournamentId,
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

        public async Task<Response> SendDeleteQualifierEvent(string tournamentId, CoreServer host, QualifierEvent qualifierEvent)
        {
            if (host.CoreServerEquals(ServerSelf))
            {
                return await DeleteQualifierEvent(tournamentId, qualifierEvent);
            }
            else
            {
                var result = await HostScraper.RequestResponse(host, new Packet
                {
                    Event = new Event
                    {
                        qualifier_deleted_event = new Event.QualifierDeletedEvent
                        {
                            TournamentGuid = tournamentId,
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

        public async Task<Response> CreateQualifierEvent(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            await QualifierDatabase.SaveModelToDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                tournament.Qualifiers.Add(qualifierEvent);
            }

            var @event = new Event
            {
                qualifier_created_event = new Event.QualifierCreatedEvent
                {
                    TournamentGuid = tournamentId,
                    Event = qualifierEvent
                }
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
            {
                Event = @event
            });

            return new Response
            {
                Type = Response.ResponseType.Success,
                modify_qualifier = new Response.ModifyQualifier
                {
                    Message = $"Successfully created event: {qualifierEvent.Name} with settings: {(QualifierEvent.EventSettings)qualifierEvent.Flags}"
                }
            };
        }

        public async Task<Response> UpdateQualifierEvent(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Update Event entry
            await QualifierDatabase.SaveModelToDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                var eventToReplace = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
                tournament.Qualifiers.Remove(eventToReplace);
                tournament.Qualifiers.Add(qualifierEvent);
            }

            var @event = new Event
            {
                qualifier_updated_event = new Event.QualifierUpdatedEvent
                {
                    TournamentGuid = tournamentId,
                    Event = qualifierEvent
                }
            };

            var updatePacket = new Packet
            {
                Event = @event
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), updatePacket);

            return new Response
            {
                Type = Response.ResponseType.Success,
                modify_qualifier = new Response.ModifyQualifier
                {
                    Message = $"Successfully updated event: {qualifierEvent.Name}"
                }
            };
        }

        public async Task<Response> DeleteQualifierEvent(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Mark all songs and scores as old
            await QualifierDatabase.DeleteFromDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                var eventToRemove = tournament.Qualifiers.FirstOrDefault(x => x.Guid == qualifierEvent.Guid);
                tournament.Qualifiers.Remove(eventToRemove);
            }

            var @event = new Event
            {
                qualifier_deleted_event = new Event.QualifierDeletedEvent
                {
                    TournamentGuid = tournamentId,
                    Event = qualifierEvent
                }
            };

            await BroadcastToAllInTournament(Guid.Parse(tournamentId), new Packet
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

        public async Task AddServer(CoreServer host)
        {
            lock (State)
            {
                var oldHosts = State.KnownServers.ToArray();
                State.KnownServers.Clear();
                State.KnownServers.AddRange(oldHosts.Union(new[] { host }, new CoreServerEqualityComparer()));

                //Save to disk
                config.SaveServers(State.KnownServers.ToArray());
            }

            var @event = new Event
            {
                server_added_event = new Event.ServerAddedEvent
                {
                    Server = host
                }
            };

            await BroadcastToAllClients(new Packet
            {
                Event = @event
            });
        }

        public async Task RemoveServer(CoreServer host)
        {
            lock (State)
            {
                var hostToRemove = State.KnownServers.FirstOrDefault(x => x.CoreServerEquals(host));
                State.KnownServers.Remove(hostToRemove);
            }

            var @event = new Event
            {
                server_deleted_event = new Event.ServerDeletedEvent
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
                    var song = QualifierDatabase.Songs.FirstOrDefault(x => x.EventId == qualifierScore.EventId.ToString() &&
                                                                  x.LevelId == qualifierScore.Parameters.Beatmap
                                                                      .LevelId &&
                                                                  x.Characteristic == qualifierScore.Parameters.Beatmap
                                                                      .Characteristic.SerializedName &&
                                                                  x.BeatmapDifficulty == qualifierScore.Parameters
                                                                      .Beatmap.Difficulty &&
                                                                  x.GameOptions == (int)qualifierScore.Parameters
                                                                      .GameplayModifiers.Options &&
                                                                  //x.PlayerOptions == (int)submitScore.Parameters.PlayerSettings.Options &&
                                                                  !x.Old);

                    if (song != null)
                    {
                        //Mark all older scores as old
                        var scores = QualifierDatabase.Scores
                            .Where(x => x.EventId == qualifierScore.EventId.ToString() &&
                                        x.LevelId == qualifierScore.Parameters.Beatmap.LevelId &&
                                        x.Characteristic == qualifierScore.Parameters.Beatmap.Characteristic
                                            .SerializedName &&
                                        x.BeatmapDifficulty == qualifierScore.Parameters.Beatmap.Difficulty &&
                                        x.GameOptions == (int)qualifierScore.Parameters.GameplayModifiers.Options &&
                                        //x.PlayerOptions == (int)submitScore.Parameters.PlayerSettings.Options &&
                                        !x.Old &&
                                        x.UserId == ulong.Parse(qualifierScore.UserId));

                        var oldHighScore = (scores.OrderBy(x => x._Score).FirstOrDefault()?._Score ?? -1);
                        if (oldHighScore < qualifierScore.Score)
                        {
                            foreach (var score in scores) score.Old = true;

                            QualifierDatabase.Scores.Add(new Database.Models.Score
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
                            await QualifierDatabase.SaveChangesAsync();
                        }

                        var newScores = QualifierDatabase.Scores
                            .Where(x => x.EventId == qualifierScore.EventId.ToString() &&
                                        x.LevelId == qualifierScore.Parameters.Beatmap.LevelId &&
                                        x.Characteristic == qualifierScore.Parameters.Beatmap.Characteristic
                                            .SerializedName &&
                                        x.BeatmapDifficulty == qualifierScore.Parameters.Beatmap.Difficulty &&
                                        x.GameOptions == (int)qualifierScore.Parameters.GameplayModifiers.Options &&
                                        //x.PlayerOptions == (int)submitScore.Parameters.PlayerSettings.Options &&
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
                        var @event = QualifierDatabase.Qualifiers.FirstOrDefault(x => x.Guid == qualifierScore.EventId.ToString());
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
                                var eventSongs = QualifierDatabase.Songs.Where(x => x.EventId == qualifierScore.EventId.ToString() && !x.Old);
                                var eventScores = QualifierDatabase.Scores.Where(x => x.EventId == qualifierScore.EventId.ToString() && !x.Old);
                                var newMessageId = await QualifierBot.SendLeaderboardUpdate(@event.InfoChannelId, @event.LeaderboardMessageId, eventScores.ToList(), eventSongs.ToList());
                                if (@event.LeaderboardMessageId != newMessageId)
                                {
                                    @event.LeaderboardMessageId = newMessageId;
                                    await QualifierDatabase.SaveChangesAsync();
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
                    else
                    {
                        connect.User.Guid = user.id.ToString();

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
                                    Message = $"Connected to {serverName}!"
                                },
                                RespondingToPacketId = packet.Id
                            }
                        });
                    }
                }
                else if (request.TypeCase == Request.TypeOneofCase.join_tournament)
                {
                    var joinTournament = request.join_tournament;
                    var tournament = GetTournamentByGuid(joinTournament.TournamentId);

                    if (!string.IsNullOrWhiteSpace(tournament.Settings.Password) && joinTournament.Password == )
                    {

                    }
                }
                else if (request.TypeCase == Request.TypeOneofCase.leaderboard_score)
                {
                    var scoreRequest = request.leaderboard_score;
                    var scores = QualifierDatabase.Scores
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
                    var @event = QualifierDatabase.Qualifiers.FirstOrDefault(x => x.Guid == scoreRequest.EventId.ToString());
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
                        await CreateMatch(@event.match_created_event.TournamentGuid, @event.match_created_event.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_updated_event:
                        await UpdateMatch(@event.match_updated_event.TournamentGuid, @event.match_updated_event.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_deleted_event:
                        await DeleteMatch(@event.match_deleted_event.TournamentGuid, @event.match_deleted_event.Match);
                        break;
                    case Event.ChangedObjectOneofCase.user_added_event:
                        await AddUser(@event.user_added_event.TournamentGuid, @event.user_added_event.User);
                        break;
                    case Event.ChangedObjectOneofCase.user_updated_event:
                        await UpdateUser(@event.user_updated_event.TournamentGuid, @event.user_updated_event.User);
                        break;
                    case Event.ChangedObjectOneofCase.user_left_event:
                        await RemoveUser(@event.user_left_event.TournamentGuid, @event.user_left_event.User);
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_created_event:
                        var createResponse = await CreateQualifierEvent(@event.qualifier_created_event.TournamentGuid, @event.qualifier_created_event.Event);
                        createResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = createResponse,
                        });
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_updated_event:
                        var updateResponse = await UpdateQualifierEvent(@event.qualifier_updated_event.TournamentGuid, @event.qualifier_updated_event.Event);
                        updateResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = updateResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_deleted_event:
                        var deleteResponse = await DeleteQualifierEvent(@event.qualifier_deleted_event.TournamentGuid, @event.qualifier_deleted_event.Event);
                        deleteResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = deleteResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.server_added_event:
                        await AddServer(@event.server_added_event.Server);
                        break;
                    case Event.ChangedObjectOneofCase.server_deleted_event:
                        await RemoveServer(@event.server_deleted_event.Server);
                        break;
                    default:
                        Logger.Error($"Unknown command received from {user.id}!");
                        break;
                }
            }
        }
    }
}