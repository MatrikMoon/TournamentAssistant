using System;
using TournamentAssistantShared.Utilities;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class Module : Attribute
    {
        public Packet.packetOneofCase PacketType { get; private set; }

        // Each packet has a differently named and typed Enum which
        // defines exactly which handler should be run. Since it's
        // differently named and typed, we'll compare it to an int
        // (thank god they're enums), and force the attribute provider
        // to give us a stub which returns the value we should switch by
        public Func<Packet, int> GetSwitchType { get; private set; }

        public Module(Packet.packetOneofCase packetType, string switchTypePath = "")
        {
            PacketType = packetType;
            GetSwitchType = (packet) =>
            {
                if (string.IsNullOrEmpty(switchTypePath))
                {
                    return 0;
                }

                var pathWithoutPacket = switchTypePath.StartsWith("packet.") ? switchTypePath["packet.".Length..] : switchTypePath;
                var pathParts = pathWithoutPacket.Split(".");

                var switchType = packet.GetProperty(pathParts[0]).GetProperty<int>(pathParts[1]);
                return switchType;
            };
        }
    }
}
