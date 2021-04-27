using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class SubmitScore
    {
        public Score Score { get; set; }
    }
}
