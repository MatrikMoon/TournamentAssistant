using TournamentAssistantShared;

namespace TAClientTest.Interop
{
    internal class TAAuthInterop
    {
        public static string GetToken(string username, string platformId)
        {
            Logger.Warning("Calling GetToken...");
            return TAAuth.TAAuth.GetToken(username, platformId);
        }
    }
}
