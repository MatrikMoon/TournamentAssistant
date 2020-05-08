using BattleSaberShared;

namespace BattleSaberCore
{
    class BattleSaberHost
    {
        public IConnection Connection;

        public void StartHost()
        {
            Connection = new BattleSaberServer(); 
            (Connection as BattleSaberServer).Start();
        }
    }
}
