using System;
using static BattleSaberShared.Packet;

namespace BattleSaberShared.Models.Packets
{
    [Serializable]
    public class ForwardingPacket
    {
        public string[] ForwardTo { get; set; }
        public PacketType Type { get; set; }
        public object SpecificPacket { get; set; }
    }
}
