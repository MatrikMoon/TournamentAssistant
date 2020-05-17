using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Command
    {
        public enum CommandTypes
        {
            Heartbeat,
            ReturnToMenu,
            DelayTest_Trigger,
            DelayTest_Finish
        }

        public CommandTypes CommandType { get; set; }
    }
}
