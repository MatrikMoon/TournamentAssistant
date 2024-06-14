using System;
using TournamentAssistantShared.Utilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class RequirePermission : Attribute
    {
        public Permissions RequiredPermission { get; private set; }

        // Each packet has a different path for TournamentId,
        // if it exists. So, we take it in in a similar manner
        // to how we take switchType from modules
        public Func<Packet, string> GetTournamentId { get; private set; }

        public RequirePermission(Permissions requiredPermission)
        {
            RequiredPermission = requiredPermission;
            GetTournamentId = (packet) =>
            {
                var (foundProperty, foundInObject) = packet.Request.FindProperty("TournamentId", 3);
                return (string)foundProperty.GetValue(foundInObject);
            };
        }
    }
}
