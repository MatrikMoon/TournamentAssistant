using System;

namespace TournamentAssistantShared.Models.Packets
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
