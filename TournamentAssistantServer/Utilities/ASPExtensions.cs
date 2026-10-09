using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TournamentAssistantShared.Models;

namespace TournamentAssistantServer.Utilities
{
    public static class ASPExtensions
    {
        public static User? GetUserFromToken(this HttpContext context)
        {
            return context.Items.TryGetValue("UserFromToken", out var user)
                ? user as User
                : null;
        }

        public static User? GetCurrentUser(this ControllerBase controller)
        {
            return controller.HttpContext.GetUserFromToken();
        }

        public static AuthorizationService.TokenKind GetTokenKind(this HttpContext context)
        {
            return context.Items.TryGetValue("TokenKind", out var tokenKind) && tokenKind is AuthorizationService.TokenKind kind
                ? kind
                : AuthorizationService.TokenKind.None;
        }
    }
}
