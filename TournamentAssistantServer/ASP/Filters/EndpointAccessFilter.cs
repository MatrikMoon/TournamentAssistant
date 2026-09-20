using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;
using TournamentAssistantServer.Database;
using TournamentAssistantServer.Utilities;

namespace TournamentAssistantServer.ASP.Filters
{
    public class EndpointAccessFilter : IAsyncActionFilter
    {
        private readonly DatabaseService databaseService;

        public EndpointAccessFilter(DatabaseService databaseService)
        {
            this.databaseService = databaseService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor is ControllerActionDescriptor action &&
                !EndpointAccessPolicy.IsEnabled(databaseService, action.MethodInfo, EndpointTransport.Rest))
            {
                context.Result = new ObjectResult("This endpoint is disabled by the server administrator")
                {
                    StatusCode = 503,
                };
                return;
            }
            await next();
        }
    }
}
