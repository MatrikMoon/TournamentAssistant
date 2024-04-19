using System;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class PacketHandlerAttribute(int switchType = 0) : Attribute
    {
        public int SwitchType { get; private set; } = switchType;
    }
}
