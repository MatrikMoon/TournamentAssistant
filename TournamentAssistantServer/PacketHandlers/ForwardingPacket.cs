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
    class ForwardingPacket
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [PacketHandler]
        public async Task ForwardPacket(Packet packet)
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

                await TAServer.ForwardTo(forwardingPacket.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(ExecutionContext.Packet.From), forwardedPacket);
            }
        }
    }
}
