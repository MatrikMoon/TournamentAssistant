using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;
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

            var packet = context.ActionArguments.Values.FirstOrDefault(v => v is Packet) as Packet;
            if (packet == null)
            {
                context.Result = new BadRequestObjectResult("Unable to derive Packet from body");
                return;
            }

            var tournamentId = _attribute.GetTournamentId(packet);

            using var tournamentDatabase = _databaseService.NewTournamentDatabaseContext();
            if (user.discord_info == null || !tournamentDatabase.IsUserAuthorized(tournamentId, user.discord_info.UserId, _attribute.RequiredPermission))
            {
                if (!tournamentDatabase.IsUserAuthorized(tournamentId, user.PlatformId, _attribute.RequiredPermission))
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }

            await next();
        }
    }
}
