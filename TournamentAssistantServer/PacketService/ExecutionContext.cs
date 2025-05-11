using System.Collections.Generic;
using TournamentAssistantServer.PacketService.Models;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantServer.PacketService
{
    public class ExecutionContext
    {
        // Commented out to temporarily force me to use only things that can be derived from REST token
        // public List<Module> Modules { get; private set; }
        public User User { get; private set; }
        // public Packet Packet { get; private set; }

        public ExecutionContext(List<Module> modules, User user, Packet packet)
        {
            // Modules = modules;
            User = user;
            // Packet = packet;
        }
    }
}
