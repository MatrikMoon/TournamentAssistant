using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Acknowledgement
    {
        public enum AcknowledgementType
        {
            MessageReceived,
            FileDownloaded
        }

        public Guid PacketId { get; set; }

        public AcknowledgementType Type { get; set; }
    }
}
