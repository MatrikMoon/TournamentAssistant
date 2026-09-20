using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Sockets;

namespace TournamentAssistantServer.Utilities
{
    public enum EndpointTransport
    {
        Websocket,
        Rest,
        Player,
    }

    public sealed class EndpointDescription
    {
        public string EndpointId { get; set; }
        public string DisplayName { get; set; }
        public string Route { get; set; }
        public bool SupportsWebsocket { get; set; }
        public bool SupportsRest { get; set; }
        public bool SupportsPlayer { get; set; }
        public bool IsCore { get; set; }
    }

    public static class EndpointAccessPolicy
    {
        private static readonly Lazy<IReadOnlyList<EndpointDescription>> EndpointDescriptions =
            new Lazy<IReadOnlyList<EndpointDescription>>(DiscoverEndpoints);

        public static string GetEndpointId(MethodInfo method) =>
            $"{method.DeclaringType.FullName}.{method.Name}";

        public static IReadOnlyList<EndpointDescription> GetEndpoints() => EndpointDescriptions.Value;

        public static bool IsEnabled(DatabaseService databaseService, MethodInfo method, EndpointTransport transport)
        {
            using var database = databaseService.NewGlobalConfigurationDatabaseContext();
            var access = database.GetEndpointAccess(GetEndpointId(method));
            if (access == null)
            {
                return true;
            }

            return transport switch
            {
                EndpointTransport.Websocket => access.WebsocketEnabled,
                EndpointTransport.Rest => access.RestEnabled,
                EndpointTransport.Player => access.PlayerEnabled,
                _ => false,
            };
        }

        public static EndpointTransport GetTransport(User user, ConnectedUser connection = null) =>
            user?.ClientType == User.ClientTypes.Player || (user == null && connection?.websocketConnection == null)
                ? EndpointTransport.Player
                : user?.ClientType == User.ClientTypes.RESTConnection
                    ? EndpointTransport.Rest
                    : EndpointTransport.Websocket;

        private static IReadOnlyList<EndpointDescription> DiscoverEndpoints()
        {
            var endpoints = new List<EndpointDescription>();
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(x => x.IsClass))
            {
                var controllerRoute = type.GetCustomAttribute<RouteAttribute>()?.Template;
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    var packetHandler = method.GetCustomAttribute<PacketHandler>();
                    var httpAttributes = method.GetCustomAttributes().OfType<HttpMethodAttribute>().ToArray();
                    var supportsRest = httpAttributes.Length > 0 && method.GetCustomAttribute<NonActionAttribute>() == null;
                    if (packetHandler == null && !supportsRest)
                    {
                        continue;
                    }

                    var route = supportsRest
                        ? string.Join(", ", httpAttributes.Select(x =>
                            $"{string.Join("/", x.HttpMethods)} {BuildRoute(controllerRoute, x.Template, type, method)}"))
                        : string.Empty;
                    endpoints.Add(new EndpointDescription
                    {
                        EndpointId = GetEndpointId(method),
                        DisplayName = $"{type.Name}.{method.Name}",
                        Route = route,
                        SupportsRest = supportsRest,
                        // Authoritative BK tokens may invoke any packet over WebSocket,
                        // including handlers normally reserved for player connections.
                        SupportsWebsocket = packetHandler != null,
                        SupportsPlayer = packetHandler != null &&
                            (method.GetCustomAttribute<AllowFromPlayer>() != null ||
                             method.GetCustomAttribute<AllowUnauthorized>() != null),
                        IsCore = method.GetCustomAttribute<CoreEndpoint>() != null,
                    });
                }
            }
            return endpoints.OrderBy(x => x.DisplayName).ToArray();
        }

        private static string BuildRoute(string controllerRoute, string methodRoute, Type controller, MethodInfo method)
        {
            var route = string.Join("/", new[] { controllerRoute, methodRoute }
                .Where(x => !string.IsNullOrWhiteSpace(x)))
                .Replace("[controller]", controller.Name.EndsWith("Controller")
                    ? controller.Name.Substring(0, controller.Name.Length - "Controller".Length)
                    : controller.Name)
                .Replace("[action]", method.Name);
            return "/" + route.Trim('/');
        }
    }
}
