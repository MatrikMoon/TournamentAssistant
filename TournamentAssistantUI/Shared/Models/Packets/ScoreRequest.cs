using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class ScoreRequest
    {
        public Guid EventId { get; set; }
        public GameplayParameters Parameters { get; set; }
    }
}
