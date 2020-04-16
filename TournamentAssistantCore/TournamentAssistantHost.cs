using TournamentAssistantShared;

namespace TournamentAssistantCore
{
    class TournamentAssistantHost
    {
        public IConnection Connection;

        public void StartHost()
        {
            Connection = new TournamentAssistantServer();
            (Connection as TournamentAssistantServer).Start();
        }
    }
}
