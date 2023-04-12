namespace TournamentAssistant.Interop
{
    internal class TAAuthInterop
    {
        public static string GetToken(string username, string platformId)
        {
            return TAAuth.TAAuth.GetToken(username, platformId);
        }
    }
}
