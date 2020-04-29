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

        public string name;
        public ConnectType clientType;
    }
}
