using System;

namespace BattleSaberShared.Models.Packets
{
    [Serializable]
    public class Connect
    {
        public enum ConnectType
        {
            Player,
            Coordinator
        }

        public ConnectType clientType;
        public string name;
        public int clientVersion;
    }
}
