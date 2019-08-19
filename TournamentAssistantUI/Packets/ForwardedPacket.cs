using System;
using static TournamentAssistantShared.Packet;

namespace TournamentAssistantUI.Packets
{
    [Serializable]
    public class ForwardedPacket
    {
        public string ForwardTo { get; set; }
        public PacketType Type { get; set; }
        public object Packet { get; set; }
    }
}
