using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using Webhook = TournamentAssistantShared.Models.Webhook;

namespace TournamentAssistantServer.PacketHandlers
{
    [Module(Packet.packetOneofCase.Push, "packet.Push.DataCase")]
    class Pushes
    {
        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }
        public StateManager StateManager { get; set; }

        [AllowFromPlayer]
        [PacketHandler((int)Push.DataOneofCase.song_finished)]
        public async Task SongFinished(Packet packet, [FromUser] User user)
        {
            TAServer.ClearReplayStream(user?.PlatformId);
            var result = packet.Push.song_finished;
            if (
                !string.IsNullOrEmpty(result?.TournamentId)
                && StateManager.GetTournament(result.TournamentId)?.Users.Any(x => x.Guid == user.Guid) == true
            )
                TAServer.PublishWebhook(
                    result.TournamentId,
                    Webhook.Trigger.SongFinished,
                    "songFinished",
                    result
                );
            await TAServer.BroadcastToAllClients(packet); //TODO: Should be targeted
        }
    }
}
