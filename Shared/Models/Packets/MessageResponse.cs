using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class MessageResponse
    {
        public Guid PacketId { get; set; }
        public string? Value { get; set; }
    }
}