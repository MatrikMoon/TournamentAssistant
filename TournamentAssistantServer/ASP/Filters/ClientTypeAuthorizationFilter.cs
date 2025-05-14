using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantServer.PacketService.Attributes;
using TournamentAssistantServer.Utilities;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 5/11/2025
 * This filter handles the permission tags that the
 * TA System already had in place before implementing
 * ASP.NET
 */

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

            // TODO: Temporarily, we're just treating the AllowFromWebsocket attribute as if it indicates
            // that REST connections should be allowed as well. Maaaybe don't do this? But as I write this
            // I'm in the middle of all the tedious work that comes with adding ASP.NET, so I can confidently
            // say that I want to handle this later.
            if (endpoint.OfType<AllowFromWebsocket>().Any() && (user?.ClientType == User.ClientTypes.WebsocketConnection) || (user?.ClientType == User.ClientTypes.RESTConnection))
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
