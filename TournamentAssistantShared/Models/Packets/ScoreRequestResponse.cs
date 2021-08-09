using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class ScoreRequestResponse
    {
        public Score[] Scores { get; set; }
    }
}
