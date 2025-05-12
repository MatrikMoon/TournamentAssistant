using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantDiscordBot.Discord;
using TournamentAssistantServer.Sockets;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using static TournamentAssistantShared.Constants;
using System.Security.Cryptography.X509Certificates;

namespace TournamentAssistantServer
{
    public class TAServer
    {
        Server server;
        OAuthServer oauthServer;

        // Track OAuth requests so that if oauth fails, we can respond with an update response
        // TODO: Remove soon, this is a hack for updating OAuth on 9/19/2024
        public Dictionary<string, string> PendingOAuthUsersPacketIds { get; private set; } = new Dictionary<string, string>();

        public event Func<Acknowledgement, Guid, Task> AckReceived;

        // The master server will maintain live connections to other servers, for the purpose of maintaining the master server
        // list and an updated list of tournaments
        private List<TAClient> ServerConnections { get; set; }

        private User Self { get; set; }

        private StateManager StateManager { get; set; }
        private PacketService.PacketService PacketService { get; set; }

        private QualifierBot QualifierBot { get; set; }
        private DatabaseService DatabaseService { get; set; }
        private AuthorizationService AuthorizationService { get; set; }

        private ServerConfig Config { get; set; }

        public X509Certificate2 GetCert()
        {
            return Config.ServerCert;
        }

        public TAServer(string botTokenArg = null)
        {
            Directory.CreateDirectory("files");
            Config = new ServerConfig(botTokenArg);
        }

        public void RegisterHandlerService(PacketService.PacketService packetService)
        {
            server.PacketReceived += packetService.ParseMessage;
        }

        // Blocks until socket server begins to start (note that this is not "until server is started")
        public async void Start(Action<IServiceCollection> onServiceCollectionCreated = null)
        {
            // Set up the databases
            DatabaseService = new DatabaseService();

            // Set up state manager
            StateManager = new StateManager(this, DatabaseService);

            // Load saved tournaments from database
            await StateManager.LoadSavedTournaments();

            // Set up Authorization Manager
            AuthorizationService = new AuthorizationService(DatabaseService, Config.ServerCert, Config.BeatKhanaPublicKey, Config.PluginCert, Config.MockCert);

            // Create the default server list
            ServerConnections = new List<TAClient>();

            // If we have a token, start a qualifier bot
            if (!string.IsNullOrEmpty(Config.BotToken) && Config.BotToken != "[botToken]")
            {
                //We need to await this so the DI framework has time to load the database service
                QualifierBot = new QualifierBot(DiscordCallback_GetTournamentsWhereUserIsAdmin, DiscordCallback_AddAuthorizedUser, botToken: Config.BotToken);
                await QualifierBot.Start();
            }

            // Give our new server a sense of self :P
            Self = new User()
            {
                Guid = Guid.Empty.ToString(),
                Name = Config.ServerName ?? "HOST"
            };

            Logger.Info("Starting the server...");

            // Set up OAuth Server if applicable settings have been set
            if (Config.OAuthPort > 0)
            {
                oauthServer = new OAuthServer(AuthorizationService, Config.Address, Config.OAuthPort, Config.OAuthClientId, Config.OAuthClientSecret);
                oauthServer.UserNeedsToUpdate += OAuthServer_UserNeedsToUpdate;
                oauthServer.Start();
            }

            // Set up event listeners
            server = new Server(Config.Port, Config.ServerCert, Config.WebsocketPort);
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
            server.PacketReceived += Server_PacketReceived_AckHandler;

            var serviceCollection = new ServiceCollection()
                .AddSingleton(this)
                .AddSingleton(StateManager)
                .AddSingleton(DatabaseService)
                .AddSingleton(AuthorizationService);

            if (QualifierBot != null)
            {
                serviceCollection.AddSingleton(QualifierBot);
            }

            var serviceProvider = serviceCollection.BuildServiceProvider();

            PacketService = new PacketService.PacketService(this, AuthorizationService, DatabaseService, oauthServer);
            PacketService.Initialize(Assembly.GetExecutingAssembly(), serviceProvider);

            server.Start();

            // Add self to known servers
            await StateManager.AddServer(new CoreServer
            {
                Address = Config.Address == "[serverAddress]" ? "127.0.0.1" : Config.Address,
                Port = Config.Port,
                WebsocketPort = Config.WebsocketPort,
                Name = Config.ServerName
            });

            // (Optional) Verify that this server can be reached from the outside
            await Verifier.VerifyServer(Config.Address, Config.Port);

            if (onServiceCollectionCreated != null)
            {
                onServiceCollectionCreated?.Invoke(serviceCollection);
            }
        }

        private async Task<List<Tournament>> DiscordCallback_GetTournamentsWhereUserIsAdmin(string userId)
        {
            var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            return await tournamentDatabase.GetTournamentsWhereUserIsAdmin(userId);
        }

        private void DiscordCallback_AddAuthorizedUser(string tournamentId, string userId, Permissions permissions)
        {
            var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();

            tournamentDatabase.AddAuthorizedUser(tournamentId, userId, permissions);
        }

        private Task OAuthServer_UserNeedsToUpdate(string userId)
        {
            var packetId = PendingOAuthUsersPacketIds[userId];

            PendingOAuthUsersPacketIds.Remove(userId);

            return Send(Guid.Parse(userId), new Packet
            {
                Response = new Response
                {
                    Type = Response.ResponseType.Fail,
                    connect = new Response.Connect
                    {
                        ServerVersion = WEBSOCKET_VERSION_CODE,
                        Message = $"Version mismatch, this server expected version {WEBSOCKET_VERSION} (TAUI version: {TAUI_VERSION})",
                        Reason = Response.Connect.ConnectFailReason.IncorrectVersion
                    },
                    RespondingToPacketId = packetId
                }
            });
        }

        public void Shutdown()
        {
            server.Shutdown();
        }

        public void AddServerConnection(TAClient serverConnection)
        {
            ServerConnections.Add(serverConnection);
        }

        private async Task Server_ClientDisconnected(ConnectedUser client)
        {
            Logger.Error($"Client Disconnected! {client.id}");

            foreach (var tournament in StateManager.GetTournaments())
            {
                var users = StateManager.GetUsers(tournament.Guid);
                var user = users.FirstOrDefault(x => x.Guid == client.id.ToString());
                if (user != null)
                {
                    await StateManager.RemoveUser(tournament.Guid, user);
                }
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
                else
                {
                    secondaryInfo = command.TypeCase.ToString();
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Request)
            {
                var request = packet.Request;
                if (request.TypeCase == Request.TypeOneofCase.load_song)
                {
                    var loadSong = request.load_song;
                    secondaryInfo = loadSong.LevelId;
                }
            }
            else if (packet.packetCase == Packet.packetOneofCase.Event)
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
                    secondaryInfo = $"{secondaryInfo} ({match.SelectedMap?.GameplayParameters.Beatmap.Difficulty})";
                }
            }
            if (packet.packetCase == Packet.packetOneofCase.ForwardingPacket)
            {
                var forwardedpacketCase = packet.ForwardingPacket.Packet.packetCase;
                secondaryInfo = $"{forwardedpacketCase}";
            }

            return $"({packet.packetCase}) ({secondaryInfo})";
        }

        public Task InvokeAckReceived(Packet packet)
        {
            return AckReceived?.Invoke(packet.Acknowledgement, Guid.Parse(packet.From));
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

        public async Task BroadcastToAllInTournament(Guid tournamentId, Packet packet)
        {
            packet.From = Self.Guid;
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            await server.Send(StateManager.GetUsers(tournamentId.ToString()).Select(x => Guid.Parse(x.Guid)).ToArray(), new PacketWrapper(packet));
        }

        public async Task BroadcastToAllClients(Packet packet)
        {
            packet.From = Self.Guid;
            Logger.Debug($"Sending data: {LogPacket(packet)}");
            await server.Broadcast(new PacketWrapper(packet));
        }

        private Task Server_PacketReceived_AckHandler(ConnectedUser user, Packet packet)
        {
            Logger.Debug($"Received data: {LogPacket(packet)}");

            //Ready to go, only disabled since it is currently unusued
            /*if (packet.packetCase != Packet.packetOneofCase.Acknowledgement)
            {
                await Send(Guid.Parse(packet.From), new Packet
                {
                    Acknowledgement = new Acknowledgement
                    {
                        PacketId = packet.Id
                    }
                });
            }*/

            return Task.CompletedTask;
        }
    }
}