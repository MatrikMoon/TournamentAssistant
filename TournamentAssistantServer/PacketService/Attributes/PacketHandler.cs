using System;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class PacketHandler : Attribute
    {
        public int SwitchType { get; private set; }

        public PacketHandler(int switchType = 0)
        {
            SwitchType = switchType;
        }
    }
}
