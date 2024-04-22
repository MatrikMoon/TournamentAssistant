using System.Collections.Generic;
using System;
using System.Reflection;

namespace TournamentAssistantServer.PacketService.Models
{
    public class PacketHandler
    {
        public MethodInfo Method { get; private set; }
        public int SwitchType { get; private set; }
        public List<Type> Parameters { get; private set; }

        public PacketHandler(MethodInfo method, int switchType, List<Type> parameters)
        {
            Method = method;
            SwitchType = switchType;
            Parameters = parameters;
        }
    }
}
