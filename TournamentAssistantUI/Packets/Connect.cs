using System;

namespace TournamentAssistantShared.Models
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
