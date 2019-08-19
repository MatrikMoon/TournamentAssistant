using System;

namespace TournamentAssistantShared.Models
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
