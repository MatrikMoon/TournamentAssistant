using System;

namespace TournamentAssistantShared.Models.Discord
{
    [Serializable]
    public class Guild
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
    }
}
