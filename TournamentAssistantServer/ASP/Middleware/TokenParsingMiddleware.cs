using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;

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
            Console.WriteLine(context.Request.Host);
            Console.WriteLine(context.Request.Path);
            Console.WriteLine(context.Request.Method);
            Console.WriteLine(context.Request.ContentLength);

            var token = context.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("BEARER WAS EMPTY?");
            }

            Console.WriteLine(token);

            User userFromToken = null;
            var tokenIsReadonly = token == "readonly";
            var tokenWasVerified = !tokenIsReadonly && _authorizationService.VerifyUser(token, null, out userFromToken);

            if (tokenIsReadonly)
            {
                userFromToken = new User
                {
                    Guid = Guid.NewGuid().ToString(),
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
