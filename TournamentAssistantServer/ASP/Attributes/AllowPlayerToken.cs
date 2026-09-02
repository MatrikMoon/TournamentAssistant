using System;

namespace TournamentAssistantServer.ASP.Attributes
{
    /// <summary>Allows a player or mock-player bearer token to be validated without a socket.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AllowPlayerToken : Attribute
    {
    }
}
