namespace TournamentAssistant.Interop
{
    internal class TAAuthInterop
    {
        public static string GetToken(string userGuid, string username, string platformId)
        {
            return TAAuth.TAAuth.GetToken(userGuid, username, platformId);
        }
    }
}
