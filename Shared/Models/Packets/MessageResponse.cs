using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class MessageResponse
    {
        public Guid PacketId { get; set; }
        #nullable enable
        public string? Value { get; set; }
        #nullable disable
    }
}