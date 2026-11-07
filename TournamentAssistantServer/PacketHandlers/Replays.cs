using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Replays;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantShared.Tarf;
using static TournamentAssistantShared.Permissions;
using Packets = TournamentAssistantShared.Models.Packets;
using TarfHeader = TournamentAssistantShared.Models.Tarf.Header;
using TarfMap = TournamentAssistantShared.Models.Tarf.Map;

namespace TournamentAssistantServer.PacketHandlers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Module(Packet.packetOneofCase.Request, "packet.Request.TypeCase")]
    public class Replays : ControllerBase
    {
        private const int MaxPageSize = 100;

        // Replays that couldn't be stored are sent along with the push, as long as that stays under the packet size limit
        private const int MaxPushedReplaySize = 12 * 1024 * 1024;

        public ExecutionContext ExecutionContext { get; set; }
        public TAServer TAServer { get; set; }
        public StateManager StateManager { get; set; }
        public DatabaseService DatabaseService { get; set; }
        public ReplayStorage ReplayStorage { get; set; }
        public ReplayUploads ReplayUploads { get; set; }

        [AllowFromPlayer]
        [PacketHandler((int)Packets.Request.TypeOneofCase.submit_replay)]
        [HttpPost]
        public async Task<ActionResult<Response.SubmitReplay>> SubmitReplay([FromBody] Request.SubmitReplay submitReplay, [FromUser] User user)
        {
            var tournament = StateManager.GetTournament(submitReplay.TournamentId);
            if (tournament == null || !StateManager.GetUsers(tournament.Guid).Any(x => x.Guid == user.Guid))
            {
                return BadRequest(new Response.SubmitReplay { Message = "You are not in that tournament" });
            }

            byte[] data;
            try
            {
                data = ReplayUploads.AddChunk(user.Guid, submitReplay);
            }
            catch (InvalidDataException e)
            {
                return BadRequest(new Response.SubmitReplay { Message = e.Message });
            }

            if (data == null)
            {
                return new Response.SubmitReplay { Message = $"Received chunk {submitReplay.ChunkIndex + 1} of {submitReplay.ChunkCount}" };
            }

            TarfHeader header;
            try
            {
                header = TarfReader.ReadHeader(data);
            }
            catch (Exception e)
            {
                return BadRequest(new Response.SubmitReplay { Message = $"Not a valid replay: {e.Message}" });
            }

            // The player and file name come from the server's view of who sent it, not from the file
            var submittedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var recordedAt = header.RecordedAtUnixMs > 0 ? header.RecordedAtUnixMs : submittedAt;
            var map = header.Map ?? new TarfMap();
            var replay = new ReplayRecording
            {
                TournamentId = tournament.Guid,
                MatchId = submitReplay.MatchId ?? "",
                FileName = TarfValues.FileName(user.PlatformId, map.Hash, map.Characteristic, map.Difficulty, recordedAt),
                PlatformId = user.PlatformId ?? "",
                PlayerName = user.Name ?? "",
                MapHash = map.Hash ?? "",
                Characteristic = map.Characteristic ?? "",
                Difficulty = TarfValues.ToBeatmapDifficulty(map.Difficulty),
                RecordedAt = recordedAt,
                SubmittedAt = submittedAt,
                ExpiresAt = submittedAt + (long)ReplayStorage.RetentionPeriod(tournament.Settings.ReplayRetention).TotalMilliseconds,
                Size = data.Length,
            };

            // Replays are only kept when the tournament asks for it. Otherwise, or when storage is down, they are only passed on to listeners once
            var stored = tournament.Settings.EnableReplayRecording && await ReplayStorage.Store(replay, data);
            if (stored)
            {
                replay.DownloadUrl = $"{TAServer.ReplayDownloadBaseUrl}/api/replays/{tournament.Guid}/{replay.Guid}";
            }
            else
            {
                replay.ExpiresAt = 0;
            }

            await PublishReplay(tournament.Guid, replay, stored, data);

            return new Response.SubmitReplay
            {
                Message = stored ? "Replay stored" : "Replay passed on without being stored",
                Replay = replay,
            };
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.DownloadReplays)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.get_replays)]
        [HttpPost]
        public async Task<ActionResult<Response.GetReplays>> GetReplays([FromBody] Request.GetReplays getReplays)
        {
            var pageSize = Math.Max(1, Math.Min(MaxPageSize, getReplays.PageSize));
            var page = Math.Max(0, getReplays.Page);
            var (replays, totalCount, available) = await ReplayStorage.List(getReplays.TournamentId, page, pageSize);

            foreach (var replay in replays)
            {
                replay.DownloadUrl = $"{TAServer.ReplayDownloadBaseUrl}/api/replays/{replay.TournamentId}/{replay.Guid}";
            }

            var response = new Response.GetReplays
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                StorageAvailable = available,
            };
            response.Replays.AddRange(replays);
            return response;
        }

        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.RemoveReplays)]
        [PacketHandler((int)Packets.Request.TypeOneofCase.delete_replay)]
        [HttpPost]
        public async Task<ActionResult<Response.DeleteReplay>> DeleteReplay([FromBody] Request.DeleteReplay deleteReplay)
        {
            if (!await ReplayStorage.Delete(deleteReplay.TournamentId, deleteReplay.ReplayGuid))
            {
                return NotFound(new Response.DeleteReplay { ReplayGuid = deleteReplay.ReplayGuid, Message = "Replay does not exist" });
            }
            return new Response.DeleteReplay { ReplayGuid = deleteReplay.ReplayGuid, Message = "Replay removed" };
        }

        public class ReplayDownload
        {
            public string TournamentId { get; set; }
            public string ReplayGuid { get; set; }
        }

        // The link handed out when a replay is submitted. Websocket tokens are accepted so the UI can download with the token it already has
        [AllowFromWebsocket]
        [RequirePermission(PermissionValues.DownloadReplays)]
        [HttpGet("/api/replays/{TournamentId}/{ReplayGuid}")]
        public async Task<IActionResult> Download([FromRoute] ReplayDownload download)
        {
            var stored = await ReplayStorage.Get(download.TournamentId, download.ReplayGuid);
            if (stored == null) return NotFound();

            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return File(stored.Value.Data, "application/octet-stream", stored.Value.Replay.FileName);
        }

        private async Task PublishReplay(string tournamentId, ReplayRecording replay, bool stored, byte[] data)
        {
            var push = new Push.ReplaySubmitted
            {
                TournamentId = tournamentId,
                Replay = replay,
                Stored = stored,
            };
            if (!stored)
            {
                if (data.Length <= MaxPushedReplaySize) push.Data = data;
                else Logger.Warning($"Replay {replay.FileName} is too large to pass on without storage, listeners only get its details");
            }

            TAServer.PublishWebhook(tournamentId, Webhook.Trigger.ReplaySubmitted, "replaySubmitted", push);

            // Only people who could download the replay anyway hear about it
            using var tournamentDatabase = DatabaseService.NewTournamentDatabaseContext();
            var recipients = StateManager.GetUsers(tournamentId)
                .Where(x => x.ClientType == TournamentAssistantShared.Models.User.ClientTypes.WebsocketConnection)
                .Where(x => new[] { x.discord_info?.UserId, x.PlatformId }
                    .Any(accountId => !string.IsNullOrWhiteSpace(accountId) && tournamentDatabase.IsUserAuthorized(tournamentId, accountId, Permissions.DownloadReplays)))
                .Select(x => Guid.Parse(x.Guid))
                .ToArray();

            if (recipients.Length > 0)
            {
                await TAServer.Send(recipients, new Packet { Push = new Push { replay_submitted = push } });
            }
        }
    }
}
