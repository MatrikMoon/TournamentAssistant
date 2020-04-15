using TournamentAssistantShared;

namespace BattleSaberCore
{
    class BattleSaberHost
    {
        public IConnection Connection;

        public void StartHost()
        {
            Connection = new TournamentAssistantServer();
            (Connection as TournamentAssistantServer).Start();
        }
    }
}
