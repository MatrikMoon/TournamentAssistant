using System;
using System.Collections.Generic;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketService.Models
{
    public class Module
    {
        public string Name { get; private set; }
        public Type Type { get; private set; }
        public Packet.packetOneofCase PacketType { get; private set; }
        public Func<Packet, int> GetSwitchType { get; set; }
        public List<PacketHandler> Handlers { get; private set; }

        public Module(string name, Type type, Packet.packetOneofCase packetType, Func<Packet, int> getSwitchType, List<PacketHandler> handlers)
        {
            Name = name;
            Type = type;
            PacketType = packetType;
            GetSwitchType = getSwitchType;
            Handlers = handlers;
        }
    }
}
