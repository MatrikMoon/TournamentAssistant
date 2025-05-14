using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 5/11/2025
 * This middleware parses the user's token into
 * a User object, and makes that object available as
 * a context item
 */

namespace TournamentAssistantServer.ASP.Middleware
{
    public class TokenParsingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly AuthorizationService _authorizationService;

        public TokenParsingMiddleware(RequestDelegate next, AuthorizationService authorizationService)
        {
            _next = next;
            _authorizationService = authorizationService;
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");

            User userFromToken = null;
            var tokenIsReadonly = token == "readonly";
            var tokenWasVerified = !tokenIsReadonly && _authorizationService.VerifyUser(token, null, out userFromToken);

            if (tokenIsReadonly)
            {
                userFromToken = new User
                {
                    Guid = null,
                    ClientType = User.ClientTypes.WebsocketConnection,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = "",
                        Username = "",
                        AvatarUrl = ""
                    }
                };
            }

            context.Items["UserFromToken"] = userFromToken;
            context.Items["TokenWasVerified"] = tokenWasVerified;
            context.Items["TokenIsReadonly"] = tokenIsReadonly;

            await _next(context);
        }
    }

}
