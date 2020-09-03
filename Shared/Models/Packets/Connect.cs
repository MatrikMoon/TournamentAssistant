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
            Scraper
        }

        public ConnectTypes ClientType { get; set; }
        public string Name { get; set; }
        public string UserId { get; set; }
        public int ClientVersion { get; set; }
    }
}
