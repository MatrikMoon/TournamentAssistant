using System.Net;
using System.Threading.Tasks;
using TournamentAssistantShared;

namespace TournamentAssistantServer
{
    internal static class Verifier
    {
        public static Task VerifyServer(string address, int port)
        {
            // Verify that the provided address points to our server
            if (IPAddress.TryParse(address, out _))
            {
                Logger.Warning($"\'{address}\' seems to be an IP address. You'll need a domain pointed to your server for it to be added to the server list");
            }
            else if (address == "[serverAddress]")
            {
                Logger.Warning("If you provide a value for \'serverAddress\' in the configuration file, your server can be added to the server list");
            }

            return Task.CompletedTask;
        }
    }
}
