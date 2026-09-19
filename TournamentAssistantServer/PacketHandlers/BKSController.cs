using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;
using TournamentAssistantServer.Webhooks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketHandlers
{
    [ApiController]
    public class BKSController : ControllerBase
    {
        public StateManager StateManager { get; set; }
        public DatabaseService DatabaseService { get; set; }

        [AllowFromWebsocket]
        [HttpPost("/bks/tournaments")]
        public async Task<ActionResult<Response.CreateBKTournament>> CreateTournament([FromBody] Request.CreateBKTournament request)
        {
            if (HttpContext.GetTokenKind() != AuthorizationService.TokenKind.BeatKhanaAuthoritative)
            {
                return Forbid();
            }
            if (request?.Tournament?.Settings == null || request.Organizer == null)
            {
                return BadRequest("Tournament settings and organizer are required");
            }
            if (!Guid.TryParse(request.BeatKhanaTournamentGuid, out _))
            {
                return BadRequest("beatKhanaTournamentGuid must be a GUID");
            }
            if (string.IsNullOrWhiteSpace(request.Organizer.DiscordId))
            {
                return BadRequest("organizer.discordId is required");
            }

            using (var links = DatabaseService.NewTABKLinkDatabaseContext())
            {
                if (links.BeatKhanaTournamentExists(request.BeatKhanaTournamentGuid))
                {
                    return Conflict("That BeatKhana tournament is already linked");
                }
            }

            var knownPermissions = Permissions.GetAllPermissions().Select(x => x.Value).ToHashSet();
            var requestedPermissions = request.OrganizerPermissions.Concat(request.Roles.SelectMany(x => x.Permissions));
            var unknownPermission = requestedPermissions.FirstOrDefault(x => !knownPermissions.Contains(x));
            if (unknownPermission != null)
            {
                return BadRequest($"Unknown permission: {unknownPermission}");
            }

            var organizerDeniedPermissions = new[]
            {
                Permissions.PermissionValues.AddAuthorizedUsers,
                Permissions.PermissionValues.UpdateAuthorizedUserRoles,
                Permissions.PermissionValues.RemoveAuthorizedUsers,
                Permissions.PermissionValues.ManageWebhooks,
                Permissions.PermissionValues.AddTournamentRole,
                Permissions.PermissionValues.SetTournamentRoleName,
                Permissions.PermissionValues.SetTournamentRolePermissions,
                Permissions.PermissionValues.RemoveTournamentRole,
                Permissions.PermissionValues.DeleteTournament,
                Permissions.PermissionValues.DeleteQualifier,
                Permissions.PermissionValues.SetQualifierEndTime,
                Permissions.PermissionValues.RefundAttempts,
                Permissions.PermissionValues.DeleteMatch,
                Permissions.PermissionValues.RemoveQualifierMap,
                Permissions.PermissionValues.RemoveTournamentTeam,
                Permissions.PermissionValues.RemoveTournamentPoolMaps,
                Permissions.PermissionValues.RemoveTournamentPools,
            };
            if (request.OrganizerPermissions.Any(organizerDeniedPermissions.Contains))
            {
                return BadRequest("The organizer role cannot receive BK-authoritative permissions");
            }

            var reservedRoleIds = new[] { "view_only", "coordinator", "player", "admin" };
            if (request.Roles.Any(x => string.IsNullOrWhiteSpace(x.RoleId)) ||
                request.Roles.Select(x => x.RoleId).Distinct().Count() != request.Roles.Count ||
                request.Roles.Any(x => reservedRoleIds.Contains(x.RoleId)))
            {
                return BadRequest("Role IDs must be unique, non-empty, and may not use a built-in role ID");
            }

            var validRoleIds = request.Roles.Select(x => x.RoleId).Concat(reservedRoleIds).ToHashSet();
            if (request.AuthorizedUsers.Select(x => x.User?.DiscordId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count() != request.AuthorizedUsers.Count ||
                request.AuthorizedUsers.Any(x => x.User == null || string.IsNullOrWhiteSpace(x.User.DiscordId) ||
                    x.User.DiscordId == request.Organizer.DiscordId || x.RoleIds.Contains("admin") || x.RoleIds.Any(y => !validRoleIds.Contains(y))))
            {
                return BadRequest("Each authorized user must have a Discord ID and valid role IDs");
            }

            foreach (var webhook in request.Webhooks)
            {
                if (!WebhookService.IsValidUrl(webhook.Url) ||
                    !WebhookService.AreValidTriggers(webhook.Triggers) ||
                    (webhook.SigningSecret?.Length ?? 0) > 512)
                {
                    return BadRequest("One or more webhook registrations are invalid");
                }
            }

            var tournament = await StateManager.CreateBKTournament(request);
            return Created($"/bks/tournaments/{request.BeatKhanaTournamentGuid}", new Response.CreateBKTournament
            {
                BeatKhanaTournamentGuid = request.BeatKhanaTournamentGuid,
                Tournament = tournament,
            });
        }
    }
}
