using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.Permissions;

// Moon's note 7/4/2025:
// As of now, we no longer allow clients to forward packet straight to clients,
// as we want to allow each command to be permission gated. That said, ForwardingPackets
// still exist, as we want to allow the recipients to respond to the sender in turn.
// As a consequence, we will not send responses to these commands.
namespace TournamentAssistantServer.PacketHandlers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Module(Packet.packetOneofCase.Command, "packet.Command.TypeCase")]
    class Commands
    {
        public TAServer TAServer { get; set; }
        public StateManager StateManager { get; set; }
        public DatabaseService DatabaseService { get; set; }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.ReturnToMenu)]
        [PacketHandler((int)Command.TypeOneofCase.ReturnToMenu)]
        [HttpPost]
        public async Task ReturnToMenu([FromBody] Packet packet, [FromUser] User user)
        {
            // Note to self: be very careful when moving these to the new parameter format. Packet ID needs to be passed to the below
            await TAServer.ForwardTo(packet.Command.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), packet);
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.PlayWithStreamSync)]
        [PacketHandler((int)Command.TypeOneofCase.DelayTestFinish)]
        [HttpPost]
        public async Task DelayTestFinish([FromBody] Packet packet, [FromUser] User user)
        {
            await TAServer.ForwardTo(packet.Command.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), packet);
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.PlayWithStreamSync)]
        [PacketHandler((int)Command.TypeOneofCase.StreamSyncShowImage)]
        [HttpPost]
        public async Task StreamSyncShowImage([FromBody] Packet packet, [FromUser] User user)
        {
            await TAServer.ForwardTo(packet.Command.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), packet);
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.PlayWithStreamSync)]
        [PacketHandler((int)Command.TypeOneofCase.show_color_for_stream_sync)]
        [HttpPost]
        public async Task ShowColorForStreamSync([FromBody] Packet packet, [FromUser] User user)
        {
            await TAServer.ForwardTo(packet.Command.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), packet);
        }

        [AllowFromWebsocket]
        [PacketHandler((int)Command.TypeOneofCase.play_song)]
        [HttpPost]
        public async Task PlaySong([FromBody] Packet packet, [FromUser] User user)
        {
            var command = packet.Command;
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            var requiredPermission = packet.Command.play_song.GameplayParameters.UseSync ? PlayWithStreamSync : Permissions.PlaySong;

            // First we'll check if they're authorized by discord id, then by steam/oculus id
            if (user.discord_info == null || !tournamentDatabase.IsUserAuthorized(command.TournamentId, user.discord_info?.UserId, requiredPermission))
            {
                if (!tournamentDatabase.IsUserAuthorized(command.TournamentId, user.PlatformId, requiredPermission))
                {
                    return;
                }
            }

            await TAServer.ForwardTo(packet.Command.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), packet);
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.ModifyGameplay)]
        [PacketHandler((int)Command.TypeOneofCase.modify_gameplay)]
        [HttpPost]
        public async Task ModifyGameplay([FromBody] Packet packet, [FromUser] User user)
        {
            await TAServer.ForwardTo(packet.Command.ForwardToes.Select(Guid.Parse).ToArray(), Guid.Parse(packet.From), packet);
        }
    }
}
