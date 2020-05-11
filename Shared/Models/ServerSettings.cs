using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class ServerSettings
    {
        public Team[] Teams { get; set; }
        public bool TournamentMode { get; set; }
    }
}