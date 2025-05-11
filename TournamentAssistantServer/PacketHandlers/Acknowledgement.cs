using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [ApiController]
    [Route("api/[controller]")]
    public class Acknowledgement : ControllerBase
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }

        [AllowFromPlayer]
        [AllowFromWebsocket]
        [AllowFromReadonly]
        [AllowUnauthorized]
        [PacketHandler]
        [HttpPost]
        public async Task AcknowledgementReceived([FromBody] Packet packet)
        {
            await TAServer.InvokeAckReceived(packet);
        }
    }
}
