using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class ServerSettings
    {
        public string ServerName { get; set; }
        public string Password { get; set; }
        public bool EnableTeams { get; set; }
        public Team[] Teams { get; set; }
        public int ScoreUpdateFrequency { get; set; }
        public string[] BannedMods { get; set; }
    }
}