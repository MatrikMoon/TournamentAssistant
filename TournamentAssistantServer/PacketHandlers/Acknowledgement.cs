using System.Threading.Tasks;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    public class Acknowledgement
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [AllowFromReadonly]
        [AllowUnauthorized]
        [PacketHandler]
        public async Task AcknowledgementReceived(Packet packet, User user)
        {
            await TAServer.InvokeAckReceived(packet);
        }
    }
}
