using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Score
    {
        public Guid EventId { get; set; }
        public GameplayParameters Parameters { get; set; }
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public int _Score { get; set; }
        public bool FullCombo { get; set; }
        public string Color { get; set; }
    }
}
