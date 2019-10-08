using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Command
    {
        public enum CommandType
        {
            Heartbeat,
            ReturnToMenu,
            DelayTest_Trigger,
            DelayTest_Finish
        }

        public CommandType commandType;
    }
}
