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
    }
}
