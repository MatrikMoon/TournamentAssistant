using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Response, "packet.Response.DetailsCase")]
    class Responses
    {
        [AllowFromPlayer]
        [AllowFromWebsocket]
        [PacketHandler((int)Response.DetailsOneofCase.show_modal)]
        public void ShowModal()
        {
            //await BroadcastToAllClients(packet); //TODO: Should be targeted
        }
    }
}
