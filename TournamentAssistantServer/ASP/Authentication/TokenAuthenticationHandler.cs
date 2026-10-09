using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using TournamentAssistantServer.Utilities;

namespace TournamentAssistantServer.ASP.Authentication
{
    /// <summary>
    /// Bridges TA's token-parsing middleware into ASP.NET Core authentication so
    /// Challenge() and Forbid() consistently produce 401/403 responses.
    /// </summary>
    public sealed class TokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TournamentAssistantToken";

        public TokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var user = Context.GetUserFromToken();
            var verified = Context.Items.TryGetValue("TokenWasVerified", out var verifiedValue) && verifiedValue is true;
            var isReadonly = Context.Items.TryGetValue("TokenIsReadonly", out var readonlyValue) && readonlyValue is true;

            if (user == null || (!verified && !isReadonly))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Guid ?? string.Empty),
                new Claim(ClaimTypes.Name, user.Name ?? user.discord_info?.Username ?? string.Empty),
                new Claim("ta:token_kind", Context.GetTokenKind().ToString()),
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
