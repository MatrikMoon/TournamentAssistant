using System.Reflection;

namespace TournamentAssistantServer.PacketService.Models
{
    public class PacketHandler
    {
        public MethodInfo Method { get; private set; }
        public int SwitchType { get; private set; }

        public PacketHandler(MethodInfo method, int switchType)
        {
            Method = method;
            SwitchType = switchType;
        }
    }
}
