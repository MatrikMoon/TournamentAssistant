using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models.Packets;

/**
 * Created by Moon on 5/11/2025
 * This filter works in conjunction with the
 * RequirePermission attribute to check a user's
 * permission on a given tournament
 */

namespace TournamentAssistantServer.ASP.Filters
{
    public class RequirePermissionFilter : IAsyncActionFilter
    {
        private readonly DatabaseService _databaseService;
        private readonly RequirePermission _attribute;

        public RequirePermissionFilter(DatabaseService databaseService, RequirePermission attribute)
        {
            _databaseService = databaseService;
            _attribute = attribute;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.GetUserFromToken();

            if (user == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var permissionPayload = context.ActionArguments.Values.FirstOrDefault(v =>
                v != null && _attribute.GetTournamentIdFromPayload(v) != null);
            if (permissionPayload == null)
            {
                context.Result = new BadRequestObjectResult("Unable to derive tournament ID from body");
                return;
            }

            var tournamentId = _attribute.GetTournamentIdFromPayload(permissionPayload);

            using var tournamentDatabase = _databaseService.NewTournamentDatabaseContext();
            
            if (user.IsMock)
            {
                var mockTournament = tournamentDatabase.Tournaments.FirstOrDefault(x => !x.Old && x.Guid == tournamentId);
                var mockPlayerPermissions = Constants.DefaultRoles.GetPlayer(tournamentId).Permissions;
                
                mockPlayerPermissions.Add(Permissions.PermissionValues.AddUserToMatch);
                mockPlayerPermissions.Add(Permissions.PermissionValues.RemoveUserFromMatch);
                
                if (mockTournament?.AllowMockClients != true ||
                    !mockPlayerPermissions.Contains(_attribute.RequiredPermission))
                {
                    context.Result = new ForbidResult();
                    return;
                }

                await next();
                return;
            }

            // BK-authoritative tokens intentionally have no user identity. This bypass must stay before all user lookups.
            if (AuthoritativeAccessPolicy.HasTournamentAccess(context.HttpContext.GetTokenKind(), tournamentId, tournamentDatabase))
            {
                await next();
                return;
            }
            if (user.discord_info == null || !tournamentDatabase.IsUserAuthorized(tournamentId, user.discord_info.UserId, Permissions.FromValue(_attribute.RequiredPermission)))
            {
                if (!tournamentDatabase.IsUserAuthorized(tournamentId, user.PlatformId, Permissions.FromValue(_attribute.RequiredPermission)))
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }

            await next();
        }
    }
}
