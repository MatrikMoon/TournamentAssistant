using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Sockets;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Sockets;
using Module = TournamentAssistantServer.PacketService.Models.Module;

/**
 * Created by Moon on 4/16/2024
 * Inspired by my Kik Client, which was
 * in turn inspired by Discord.NET, this
 * allows us to use attributes to specify permission
 * requirements and such
 */

namespace TournamentAssistantServer.PacketService
{
    public class PacketService
    {
        private List<Module> Modules { get; set; } = new List<Module>();
        private TAServer Server { get; set; }
        private AuthorizationService AuthorizationService { get; set; }
        private DatabaseService DatabaseService { get; set; }
        private OAuthServer OAuthServer { get; set; }
        private IServiceProvider Services { get; set; }

        public PacketService(TAServer server, AuthorizationService authorizationService, DatabaseService databaseService, OAuthServer oAuthServer)
        {
            AuthorizationService = authorizationService;
            DatabaseService = databaseService;
            OAuthServer = oAuthServer;
            Server = server;
            Server.RegisterHandlerService(this);
        }

        /// <summary>
        /// Initializes the CommandServices by searching the provided assembly for
        /// the relevant attributes and building the corresponding model list
        /// </summary>
        /// <param name="assembly">The assembly to be searched for command modules</param>
        public void Initialize(Assembly assembly, IServiceProvider services = null)
        {
            Services = services;

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsClass)
                {
                    var moduleAttribute = type.GetCustomAttribute<Attributes.Module>();
                    if (moduleAttribute != null)
                    {
                        var handlersInModule = new List<Models.PacketHandler>();
                        foreach (var method in type.GetMethods())
                        {
                            var handlerAttribute = method.GetCustomAttribute<PacketHandler>();
                            if (handlerAttribute != null)
                            {
                                var parameterTypes = method.GetParameters().Select(x => x.ParameterType);
                                handlersInModule.Add(new Models.PacketHandler(method, handlerAttribute.SwitchType, parameterTypes.ToList()));
                            }
                        }

                        Modules.Add(new Module(type.Name, type, moduleAttribute.PacketType, moduleAttribute.GetSwitchType, handlersInModule));
                    }
                }
            }
        }

        /// <summary>
        /// Parses a packet and invokes the appropriate registered commands.
        /// </summary>
        /// <param name="packet">The packet to parse</param>
        /// <returns></returns>
        public async Task ParseMessage(ConnectedUser user, Packet packet)
        {
            User userFromToken = null;
            var tokenIsReadonly = packet.Token == "readonly";
            var tokenWasVerified = !tokenIsReadonly && AuthorizationService.VerifyUser(packet.Token, user, out userFromToken);

            if (tokenIsReadonly)
            {
                userFromToken = new User
                {
                    Guid = user.id.ToString(),
                    ClientType = User.ClientTypes.WebsocketConnection,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = "",
                        Username = "",
                        AvatarUrl = ""
                    }
                };
            }

            // Handle method attributes
            async Task HandleAttributes(Models.PacketHandler handler, Func<Task> runIfNoActionNeeded)
            {
                // If the command requires a permission, check that the user has that
                // permission for the tournament
                var permissionAttribute = handler.Method.GetCustomAttribute<RequirePermission>();
                if (permissionAttribute != null)
                {
                    using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
                    var tournamentId = permissionAttribute.GetTournamentId(packet);
                    if (!tournamentDatabase.IsUserAuthorized(tournamentId, userFromToken.discord_info.UserId, permissionAttribute.RequiredPermission))
                    {
                        return;
                    }
                }

                // Check that the command can be accessed by this type of user
                if ((handler.Method.GetCustomAttribute(typeof(AllowFromPlayer)) != null && tokenWasVerified && userFromToken.ClientType == User.ClientTypes.Player) ||
                    (handler.Method.GetCustomAttribute(typeof(AllowFromWebsocket)) != null && tokenWasVerified && userFromToken.ClientType == User.ClientTypes.WebsocketConnection) ||
                    (handler.Method.GetCustomAttribute(typeof(AllowFromReadonly)) != null && tokenIsReadonly) ||
                    (handler.Method.GetCustomAttribute(typeof(AllowUnauthorized)) != null))
                {
                    await runIfNoActionNeeded();
                }
                else
                {
                    // If the packet failed all of the above cases, the user needs to be authorized
                    await Server.Send(user.id, new Packet
                    {
                        Command = new Command
                        {
                            DiscordAuthorize = OAuthServer.GetOAuthUrl(user.id.ToString())
                        }
                    });
                }
            }

            // If a method is async, return the Task. If not, invoke and return CompletedTask
            async Task InvokeMethodAsAsync(MethodInfo method, object instance, params object[] parameters)
            {
                try
                {
                    if (method.ReturnType == typeof(Task))
                    {
                        await (method.Invoke(instance, parameters) as Task);
                    }
                    else
                    {
                        await Task.FromResult(method.Invoke(instance, parameters));
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }

            foreach (var module in Modules.Where(x => x.PacketType == packet.packetCase))
            {
                var switchType = module.GetSwitchType(packet);

                // For every handler that has a matching type...
                foreach (var handler in module.Handlers.Where(x => x.SwitchType == switchType))
                {
                    var parameters = new List<object>();
                    var expectedParameters = new List<Type>(handler.Parameters);
                    var incompatibleParameterType = false;

                    // Here we provide requested parameters for handlers
                    for (var i = 0; i < expectedParameters.Count && !incompatibleParameterType; i++)
                    {
                        if (expectedParameters[i] == typeof(User))
                        {
                            parameters.Add(userFromToken);
                        }
                        else if (expectedParameters[i] == typeof(Packet))
                        {
                            parameters.Add(packet);
                        }
                        else
                        {
                            Logger.Error($"{module.Name} module's {handler.Method.Name} handler has incompatible parameter: {expectedParameters[i].GetType()}");
                            incompatibleParameterType = true;
                        }
                    }

                    if (!incompatibleParameterType)
                    {
                        await HandleAttributes(handler, async () =>
                        {
                            var context = new ExecutionContext(Modules, userFromToken, packet);
                            var instantiatedModule = module.Type.CreateWithServices(Services, context);
                            await InvokeMethodAsAsync(handler.Method, instantiatedModule, parameters.ToArray());
                        });
                    }
                }
            }
        }
    }
}
