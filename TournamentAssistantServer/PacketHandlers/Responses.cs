using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.Database.Models;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Response, "packet.Response.DetailsCase")]
    class Responses
    {
        [AllowFromPlayer]
        [AllowFromBKGameToken]
        [AllowFromWebsocket]
        [PacketHandler((int)Response.DetailsOneofCase.show_prompt)]
        public async Task ShowPrompt([FromBody] Response.ShowPrompt showPrompt, [FromUser] User user)
        {
            // Moon's note, 5/24/2026: This seems to be unused? Looks like the plugin sends the responses directly
            // back to the original sender via ForwardingPacket. Can't remember if that's good practice or not.
            // Leaving this as a reminder to investigate
            // await BroadcastToAllClients(packet); //TODO: Should be targeted
        }
    }
}
