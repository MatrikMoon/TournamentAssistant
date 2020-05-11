using TournamentAssistantShared;

namespace TournamentAssistantCore
{
    class SystemHost
    {
        public IConnection Connection;

        public void StartHost()
        {
            Connection = new SystemServer(); 
            (Connection as SystemServer).Start();
        }
    }
}
