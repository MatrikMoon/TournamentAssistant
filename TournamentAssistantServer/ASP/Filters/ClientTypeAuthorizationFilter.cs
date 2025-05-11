using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared.Models;

namespace TournamentAssistantServer.ASP.Filters
{
    public class ClientTypeAuthorizationFilter : IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.GetUserFromToken();
            var endpoint = context.ActionDescriptor.EndpointMetadata;

            if (endpoint.OfType<AllowUnauthorized>().Any())
            {
                return Task.CompletedTask;
            }

            if (user == null)
            {
                context.Result = new UnauthorizedResult();
                return Task.CompletedTask;
            }

            if (endpoint.OfType<AllowFromPlayer>().Any() && user?.ClientType == User.ClientTypes.Player)
            {
                return Task.CompletedTask;
            }

            if (endpoint.OfType<AllowFromWebsocket>().Any() && user?.ClientType == User.ClientTypes.WebsocketConnection)
            {
                return Task.CompletedTask;
            }

            if (endpoint.OfType<AllowFromReadonly>().Any() && context.HttpContext.Items.TryGetValue("TokenIsReadonly", out var readonlyFlag) && readonlyFlag is true)
            {
                return Task.CompletedTask;
            }

            context.Result = new ForbidResult();
            return Task.CompletedTask;
        }
    }

}
