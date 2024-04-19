using System;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class PacketHandlerAttribute : Attribute
    {
        public int SwitchType { get; private set; }

        public PacketHandlerAttribute(int switchType = 0)
        {
            SwitchType = switchType;
        }
    }
}
