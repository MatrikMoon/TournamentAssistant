using Open.Nat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantServer.Discord;
using TournamentAssistantServer.Discord.Helpers;
using TournamentAssistantServer.Discord.Services;
using TournamentAssistantServer.Sockets;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using TournamentAssistantShared.Utilities;
using static TournamentAssistantShared.Constants;

namespace TournamentAssistantServer
{
    public class TAServer
    {
        Server server;
        OAuthServer oauthServer;

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

        AuthorizationManager AuthorizationManager { get; set; }
        public QualifierBot QualifierBot { get; private set; }
        public QualifierDatabaseContext QualifierDatabase { get; private set; }
        public TournamentDatabaseContext TournamentDatabase { get; private set; }
        public UserDatabaseContext UserDatabase { get; private set; }


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
        private int websocketPort;

        //Oauth Settings
        private int oauthPort;
        private string oauthClientId;
        private string oauthClientSecret;

        //Keys
        private X509Certificate2 cert = new("server.pfx", "password");

        public TAServer(string botTokenArg = null)
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

            var oauthPortValue = config.GetString("oauthPort");
            if (oauthPortValue == string.Empty || oauthPortValue == "[oauthPort]")
            {
                oauthPortValue = "2054";
                config.SaveString("oauthPort", oauthPortValue);
            }

            var discordClientId = config.GetString("discordClientId");
            if (discordClientId == string.Empty)
            {
                discordClientId = string.Empty;
                config.SaveString("discordClientId", "[discordClientId]");
            }

            var discordClientSecret = config.GetString("discordClientSecret");
            if (discordClientSecret == string.Empty)
            {
                discordClientSecret = string.Empty;
                config.SaveString("discordClientSecret", "[discordClientSecret]");
            }

            var botTokenValue = config.GetString("botToken");
            if (botTokenValue == string.Empty || botTokenValue == "[botToken]")
            {
                botTokenValue = botTokenArg;
                config.SaveString("botToken", "[botToken]");
            }

            address = addressValue;
            port = int.Parse(portValue);
            websocketPort = int.Parse(overlayPortValue);
            oauthPort = int.Parse(oauthPortValue);
            oauthClientId = discordClientId;
            oauthClientSecret = discordClientSecret;
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
                        Entry.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    }
                    else
                    {
                        Logger.Warning("Update was successful, exitting...");
                        Entry.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
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
                TournamentDatabase = QualifierBot.TournamentDatabase;
                QualifierDatabase = QualifierBot.QualifierDatabase;
                UserDatabase = QualifierBot.UserDatabase;
            }
            else
            {
                //If the bot's not running, we need to start the service manually
                var service = new DatabaseService();
                TournamentDatabase = service.TournamentDatabaseContext;
                QualifierDatabase = service.QualifierDatabaseContext;
                UserDatabase = service.UserDatabaseContext;
            }

            //Set up Authorization Manager
            AuthorizationManager = new AuthorizationManager(UserDatabase, cert);

            //Translate Events and Songs from database to model format
            //Don't need to lock this since it happens on startup
            foreach (var tournament in TournamentDatabase.Tournaments)
            {
                State.Tournaments.Add(await TournamentDatabase.LoadModelFromDatabase(tournament));
            }

            foreach (var tournament in State.Tournaments)
            {
                tournament.Qualifiers.AddRange(await QualifierDatabase.LoadModelsFromDatabase(tournament));
            }

            //Give our new server a sense of self :P
            Self = new User()
            {
                Guid = Guid.Empty.ToString(),
                Name = serverName ?? "HOST"
            };

            async Task scrapeServersAndStart(CoreServer core)
            {
                ServerSelf = core ?? new CoreServer
                {
                    Address = address == "[serverAddress]" ? "127.0.0.1" : address,
                    Port = port,
                    WebsocketPort = websocketPort,
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
                await OpenPort(websocketPort);

                server = new Server(port, cert, websocketPort);
                server.PacketReceived += Server_PacketReceived;
                server.ClientConnected += Server_ClientConnected;
                server.ClientDisconnected += Server_ClientDisconnected;
                server.Start();

                if (oauthPort > 0)
                {
                    await OpenPort(oauthPort);
                    oauthServer = new OAuthServer(address, oauthPort, oauthClientId, oauthClientSecret);
                    oauthServer.AuthorizeRecieved += OAuthServer_AuthorizeRecieved;
                    oauthServer.Start();
                }

                if (gotRelease)
                {
                    //Start a regular check for updates
                    Update.PollForUpdates(() =>
                    {
                        server.Shutdown();
                        Entry.MainThreadStop.Set(); //Release the main thread, so we don't leave behind threads
                    }, updateCheckToken.Token);
                }
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

                var verificationServer = new Server(port, cert);
                verificationServer.PacketReceived += (_, packet) =>
                {
                    if (packet.packetCase == Packet.packetOneofCase.Request && packet.Request.TypeCase == Request.TypeOneofCase.connect)
                    {
                        //TODO: At the moment, any connection will trigger verification... This should be improved
                        verified = true;
                        connected.Set();
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
                        WebsocketPort = websocketPort,
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

        private async Task OAuthServer_AuthorizeRecieved(User.DiscordInfo discordInfo, string userId)
        {
            using (var httpClient = new HttpClient())
            using (var memoryStream = new MemoryStream())
            {
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{discordInfo.UserId}/{discordInfo.AvatarUrl}.png";
                var avatarStream = await httpClient.GetStreamAsync(avatarUrl);
                await avatarStream.CopyToAsync(memoryStream);

                /*var user = State.Users.FirstOrDefault(x => x.Guid == userId);
                user.Name = discordInfo.Username;
                user.discord_info = discordInfo;
                user.Info.UserImage = memoryStream.ToArray();

                await UpdateUser(user);*/

                //We generate a new GUID here, since we should not rely on the user's provided one for signing the token.
                //This new one will be used for the user from here on out
                //TODO: Can't a user still get this far with a token that's in-use, and cause an authorized event to
                //be sent to either themselves or the user they're spoofing? Not sure what that would do... But worth
                //thinking about
                var user = new User
                {
                    Guid = Guid.NewGuid().ToString(),
                    discord_info = discordInfo,
                };

                //Give the newly connected player their Self and State
                await Send(Guid.Parse(userId), new Packet
                {
                    Push = new Push
                    {
                        discord_authorized = new Push.DiscordAuthorized
                        {
                            Success = true,
                            Token = AuthorizationManager.GenerateToken(user)
                        }
                    }
                });
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
                if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.user_updated)
                {
                    var user = @event.user_updated.User;
                    secondaryInfo =
                        $"{secondaryInfo} from ({user.Name} : {user.DownloadState}) : ({user.PlayState} : {user.StreamDelayMs})";
                }
                else if (@event.ChangedObjectCase == Event.ChangedObjectOneofCase.match_updated)
                {
                    var match = @event.match_updated.Match;
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

            //Normally we would assign a random GUID here, but for users we're
            //using the same GUID that's used in the lower level socket classes.
            //TL;DR: Don't touch it

            lock (tournament.Users)
            {
                tournament.Users.Add(user);
            }

            var @event = new Event
            {
                user_added = new Event.UserAdded
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
                user_updated = new Event.UserUpdated
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
                user_left = new Event.UserLeft
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

            //Assign a random GUID here, since it should not be the client's responsibility
            match.Guid = Guid.NewGuid().ToString();

            lock (tournament.Matches)
            {
                tournament.Matches.Add(match);
            }

            var @event = new Event
            {
                match_created = new Event.MatchCreated
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
                match_updated = new Event.MatchUpdated
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
                match_deleted = new Event.MatchDeleted
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

        public async Task<Response> CreateQualifierEvent(string tournamentId, QualifierEvent qualifierEvent)
        {
            var tournament = GetTournamentByGuid(tournamentId);

            //Assign a random GUID here, since it should not be the client's responsibility
            qualifierEvent.Guid = Guid.NewGuid().ToString();

            await QualifierDatabase.SaveModelToDatabase(qualifierEvent);

            lock (tournament.Qualifiers)
            {
                tournament.Qualifiers.Add(qualifierEvent);
            }

            var @event = new Event
            {
                qualifier_created = new Event.QualifierCreated
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
                qualifier_updated = new Event.QualifierUpdated
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
                qualifier_deleted = new Event.QualifierDeleted
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

        public async Task<Response> CreateTournament(Tournament tournament)
        {
            //Assign a random GUID here, since it should not be the client's responsibility
            tournament.Guid = Guid.NewGuid().ToString();

            await TournamentDatabase.SaveModelToDatabase(tournament);

            lock (State.Tournaments)
            {
                State.Tournaments.Add(tournament);
            }

            var @event = new Event
            {
                tournament_created = new Event.TournamentCreated
                {
                    Tournament = tournament
                }
            };

            await BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            return new Response
            {
                Type = Response.ResponseType.Success,
                modify_tournament = new Response.ModifyTournament
                {
                    Message = $"Successfully created tournament: {tournament.Settings.TournamentName}"
                }
            };
        }

        public async Task<Response> UpdateTournament(Tournament tournament)
        {
            //Update Event entry
            await TournamentDatabase.SaveModelToDatabase(tournament);

            lock (State.Tournaments)
            {
                var tournamentToReplace = State.Tournaments.FirstOrDefault(x => x.Guid == tournament.Guid);
                State.Tournaments.Remove(tournamentToReplace);
                State.Tournaments.Add(tournament);
            }

            var @event = new Event
            {
                tournament_updated = new Event.TournamentUpdated
                {
                    Tournament = tournament
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
                modify_tournament = new Response.ModifyTournament
                {
                    Message = $"Successfully updated tournament: {tournament.Settings.TournamentName}"
                }
            };
        }

        public async Task<Response> DeleteTournament(Tournament tournament)
        {
            //Mark all songs and scores as old
            await TournamentDatabase.DeleteFromDatabase(tournament);

            lock (State.Tournaments)
            {
                var tournamentToRemove = State.Tournaments.FirstOrDefault(x => x.Guid == tournament.Guid);
                State.Tournaments.Remove(tournamentToRemove);
            }

            var @event = new Event
            {
                tournament_deleted = new Event.TournamentDeleted
                {
                    Tournament = tournament,
                }
            };

            await BroadcastToAllClients(new Packet
            {
                Event = @event
            });

            return new Response
            {
                Type = Response.ResponseType.Success,
                modify_tournament = new Response.ModifyTournament
                {
                    Message = $"Successfully ended tournament: {tournament.Settings.TournamentName}"
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
                server_added = new Event.ServerAdded
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
                server_deleted = new Event.ServerDeleted
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

            //Authorization
            if (!AuthorizationManager.VerifyUser(packet.Token) && packet.packetCase != Packet.packetOneofCase.Acknowledgement)
            {
                //If the user is not an automated connection, trigger authorization from them
                await Send(user.id, new Packet
                {
                    Command = new Command
                    {
                        DiscordAuthorize = oauthServer.GetOAuthUrl(user.id.ToString())
                    }
                });
                return;
            }

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
                                        !x.Old)
                            .OrderByDescending(x => x._Score)
                            .Take(10)
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
                        //Give the newly connected player the sanitized state

                        //Don't expose tourney info unless the tourney is joined
                        var sanitizedState = new State();
                        sanitizedState.Tournaments.AddRange(State.Tournaments.Select(x => new Tournament
                        {
                            Guid = x.Guid,
                            Settings = x.Settings
                        }));
                        sanitizedState.KnownServers.AddRange(State.KnownServers);

                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                Type = Response.ResponseType.Success,
                                connect = new Response.Connect
                                {
                                    State = sanitizedState,
                                    ServerVersion = VERSION_CODE
                                },
                                RespondingToPacketId = packet.Id
                            }
                        });
                    }
                }
                if (request.TypeCase == Request.TypeOneofCase.join)
                {
                    var join = request.join;
                    var tournament = GetTournamentByGuid(join.TournamentId);

                    if (await TournamentDatabase.VerifyHashedPassword(tournament.Guid, join.Password))
                    {
                        //By the time we're here, we've already confirmed that the user has a valid authorization
                        //token (yes, I know authorization is not authentication), so we'll assign their guid
                        //to the one provided in the token, as well as the token's discord information
                        //join.User.Guid = packet.Token;
                        join.User.Guid = user.id.ToString();
                        await AddUser(tournament.Guid, join.User);

                        //Don't expose other tourney info
                        var sanitizedState = new State();
                        sanitizedState.Tournaments.AddRange(
                            State.Tournaments
                                .Where(x => x.Guid != tournament.Guid)
                                .Select(x => new Tournament
                                {
                                    Guid = x.Guid,
                                    Settings = x.Settings
                                }));
                        sanitizedState.Tournaments.Add(tournament);
                        sanitizedState.KnownServers.AddRange(State.KnownServers);

                        await Send(user.id, new Packet
                        {
                            Response = new Response
                            {
                                Type = Response.ResponseType.Success,
                                join = new Response.Join
                                {
                                    SelfGuid = user.id.ToString(),
                                    State = sanitizedState,
                                    Message = $"Connected to {tournament.Settings.TournamentName}!"
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
                                join = new Response.Join
                                {
                                    Message = $"Incorrect password for {tournament.Settings.TournamentName}!",
                                    Reason = Response.JoinFailReason.IncorrectPassword
                                },
                                RespondingToPacketId = packet.Id
                            }
                        });
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
                    case Event.ChangedObjectOneofCase.match_created:
                        await CreateMatch(@event.match_created.TournamentGuid, @event.match_created.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_updated:
                        await UpdateMatch(@event.match_updated.TournamentGuid, @event.match_updated.Match);
                        break;
                    case Event.ChangedObjectOneofCase.match_deleted:
                        await DeleteMatch(@event.match_deleted.TournamentGuid, @event.match_deleted.Match);
                        break;
                    case Event.ChangedObjectOneofCase.user_added:
                        //await AddUser(@event.user_added.TournamentGuid, @event.user_added.User);
                        throw new Exception("Why is this ever happening");
                    case Event.ChangedObjectOneofCase.user_updated:
                        await UpdateUser(@event.user_updated.TournamentGuid, @event.user_updated.User);
                        break;
                    case Event.ChangedObjectOneofCase.user_left:
                        //await RemoveUser(@event.user_left.TournamentGuid, @event.user_left.User);
                        throw new Exception("Why is this ever happening");
                    case Event.ChangedObjectOneofCase.qualifier_created:
                        var createQualifierResponse = await CreateQualifierEvent(@event.qualifier_created.TournamentGuid, @event.qualifier_created.Event);
                        createQualifierResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = createQualifierResponse,
                        });
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_updated:
                        var updateQualifierResponse = await UpdateQualifierEvent(@event.qualifier_updated.TournamentGuid, @event.qualifier_updated.Event);
                        updateQualifierResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = updateQualifierResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.qualifier_deleted:
                        var deleteQualifierResponse = await DeleteQualifierEvent(@event.qualifier_deleted.TournamentGuid, @event.qualifier_deleted.Event);
                        deleteQualifierResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = deleteQualifierResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.tournament_created:
                        var createTournamentResponse = await CreateTournament(@event.tournament_created.Tournament);
                        createTournamentResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = createTournamentResponse,
                        });
                        break;
                    case Event.ChangedObjectOneofCase.tournament_updated:
                        var updateTournamentResponse = await UpdateTournament(@event.tournament_updated.Tournament);
                        updateTournamentResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = updateTournamentResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.tournament_deleted:
                        var deleteTournamentResponse = await DeleteTournament(@event.tournament_deleted.Tournament);
                        deleteTournamentResponse.RespondingToPacketId = packet.Id;
                        await Send(user.id, new Packet
                        {
                            Response = deleteTournamentResponse
                        });
                        break;
                    case Event.ChangedObjectOneofCase.server_added:
                        await AddServer(@event.server_added.Server);
                        break;
                    case Event.ChangedObjectOneofCase.server_deleted:
                        await RemoveServer(@event.server_deleted.Server);
                        break;
                    default:
                        Logger.Error($"Unknown command received from {user.id}!");
                        break;
                }
            }
        }
    }
}