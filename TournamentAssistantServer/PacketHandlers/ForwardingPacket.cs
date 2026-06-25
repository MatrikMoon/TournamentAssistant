using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.ForwardingPacket)]
    public class ForwardingPacket : ControllerBase
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }

        // Moon's note, 7/4/2025:
        // This is now only used by Players. Incoming packets destined to be forwarded to players now
        // run through Commands.cs
        [AllowFromPlayer]
        [PacketHandler]
        public async Task ForwardPacket([FromBody] Packet packet)
        {
            var forwardingPacket = packet.ForwardingPacket;
            var forwardedPacket = forwardingPacket.Packet;

            if (forwardingPacket == null)
            {
                Logger.Error("FORWARDINGPACKET NULL FOR SOME REASON");
            }
            else
            {
                // Logger.Warning($"FROM {ExecutionContext.User.Guid} {ExecutionContext.User.Name}");
                // Logger.Warning($"TO {forwardingPacket.ForwardToes.First()}");
                // Logger.Warning($"FORWARDING {forwardedPacket.packetCase} TO {forwardingPacket.ForwardToes.First()}");

                await TAServer.ForwardTo(forwardingPacket.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), forwardedPacket);
            }
        }
    }
}
