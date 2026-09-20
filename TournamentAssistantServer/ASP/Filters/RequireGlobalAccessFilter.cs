using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;

namespace TournamentAssistantServer.ASP.Filters
{
    public class RequireGlobalAccessFilter : IAsyncActionFilter
    {
        private readonly DatabaseService databaseService;
        private readonly RequireGlobalAccess attribute;

        public RequireGlobalAccessFilter(DatabaseService databaseService, RequireGlobalAccess attribute)
        {
            this.databaseService = databaseService;
            this.attribute = attribute;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var discordId = context.HttpContext.GetUserFromToken()?.discord_info?.UserId;
            using var database = databaseService.NewGlobalConfigurationDatabaseContext();
            var allowed = attribute.Requirement == GlobalAccessRequirement.FullAccess
                ? database.HasFullAccess(discordId)
                : database.CanManageEndpoints(discordId);
            if (!allowed)
            {
                context.Result = new ForbidResult();
                return;
            }
            await next();
        }
    }
}
