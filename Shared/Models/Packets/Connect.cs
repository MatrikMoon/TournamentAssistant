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

        public ConnectType clientType;
        public string name;
        public ulong userId;
        public int clientVersion;
    }
}
