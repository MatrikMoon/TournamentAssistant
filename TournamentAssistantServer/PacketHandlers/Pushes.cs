using System.Threading.Tasks;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Push, "packet.Push.DataCase")]
    class Pushes
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }

        [AllowFromPlayer]
        [PacketHandler((int)Push.DataOneofCase.song_finished)]
        public async Task SongFinished()
        {
            await TAServer.BroadcastToAllClients(ExecutionContext.Packet); //TODO: Should be targeted
        }
    }
}
