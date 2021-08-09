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
            ScreenOverlay_ShowPng,
            ScreenOverlay_ShowGreen,
            DelayTest_Finish
        }

        public CommandTypes CommandType { get; set; }
    }
}
