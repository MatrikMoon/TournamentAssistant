using System;
using TournamentAssistantShared.Models.Discord;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class SendBotMessage
    {
        public Channel Channel { get; set; }
        public string Message { get; set; }
    }
}