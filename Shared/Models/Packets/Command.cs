using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Command
    {
        public enum CommandType
        {
            Heartbeat,
            ReturnToMenu
        }

        public CommandType commandType;
    }
}
