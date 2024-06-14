using TournamentAssistantShared;

namespace TournamentAssistant.Interop
{
    internal class TAAuthInterop
    {
        public static string GetToken(string username, string platformId)
        {
            Logger.Warning("Calling GetToken...");
            var token = TAAuth.TAAuth.GetToken(username, platformId);
            Logger.Info($"Token: {token.Substring(0, 10)}...");
            return token;
        }
    }
}
