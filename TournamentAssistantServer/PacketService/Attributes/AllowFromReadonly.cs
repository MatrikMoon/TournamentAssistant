using System;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class AllowFromReadonly : Attribute
    {
    }
}
