using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class ServerSettings
    {
        public Team[] Teams { get; set; }
        public bool TournamentMode { get; set; }
        public int ScoreUpdateFrequency { get; set; }
        public string[] BannedMods { get; set; }
    }
}