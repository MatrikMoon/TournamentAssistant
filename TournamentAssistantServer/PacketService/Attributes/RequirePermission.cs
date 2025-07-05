using System;
using TournamentAssistantShared.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TournamentAssistantServer.ASP.Filters;
using TournamentAssistantServer.Database;
using static TournamentAssistantShared.Constants;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class RequirePermission : Attribute, IFilterFactory
    {
        public string RequiredPermission { get; private set; }

        // Each packet has a different path for TournamentId,
        // if it exists. So, we take it in in a similar manner
        // to how we take switchType from modules
        public Func<Packet, string> GetTournamentId { get; private set; }

        public bool IsReusable => false;

        public RequirePermission(string requiredPermission)
        {
            RequiredPermission = requiredPermission;
            GetTournamentId = (packet) =>
            {
                var (foundProperty, foundInObject) = packet.Request.FindProperty("TournamentId", 3);
                return (string)foundProperty.GetValue(foundInObject);
            };
        }

        // This is specifically for ASP.NET. See: RequirePermissionFilter.cs
        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return new RequirePermissionFilter(serviceProvider.GetRequiredService<DatabaseService>(), this);
        }
    }
}
