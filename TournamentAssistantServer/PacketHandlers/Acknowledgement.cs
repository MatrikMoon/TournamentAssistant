using System.Threading.Tasks;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Acknowledgement)]
    class Acknowledgement
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [AllowFromReadonly]
        [AllowUnauthorized]
        [PacketHandler]
        public Task AcknowledgementReceived(Packet packet)
        {
            return TAServer.InvokeAckReceived(packet);
        }
    }
}
