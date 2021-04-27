using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Connect
    {
        public enum ConnectTypes
        {
            Player,
            Coordinator,
            TemporaryConnection
        }

        public ConnectTypes ClientType { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string UserId { get; set; }
        public int ClientVersion { get; set; }
    }
}
