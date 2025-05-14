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
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Module(Packet.packetOneofCase.ForwardingPacket)]
    public class ForwardingPacket : ControllerBase
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }

        // Moon's note, 5/11/2025:
        // *Should* a REST user be able to forward packets?
        // They can't really get a response... At least without a lot of refactoring.
        // Maybe I'll table this one. Or the responses at least.
        [AllowFromPlayer]
        [AllowFromWebsocket]
        [PacketHandler]
        [HttpPost]
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
                // Logger.Warning($"FROM {ExecutionContext.Packet.From}");
                // Logger.Warning($"TO {forwardingPacket.ForwardToes.First()}");
                // Logger.Warning($"FORWARDING {forwardedPacket.packetCase} TO {forwardingPacket.ForwardToes.First()}");

                await TAServer.ForwardTo(forwardingPacket.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), forwardedPacket);
            }
        }
    }
}
