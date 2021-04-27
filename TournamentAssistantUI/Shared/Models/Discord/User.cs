using System;

namespace TournamentAssistantShared.Models.Discord
{
    [Serializable]
    public class User
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
    }
}
