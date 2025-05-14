using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;
using TournamentAssistantShared.Models.Packets;

/**
 * Created by Moon on 5/11/2025
 * This filter automatically populates a Packet's
 * `token` field when deserializing from JSON in ASP.NET
 */

namespace TournamentAssistantServer.ASP.Filters
{
    public class PopulatePacketFieldsFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var token = httpContext.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");

            foreach (var arg in context.ActionArguments)
            {
                if (arg.Value is Packet packet)
                {
                    packet.Token = token;
                }
            }

            await next();
        }
    }

}
