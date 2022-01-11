using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Message
    {
        public Guid Id { get; set; }
        public string MessageTitle { get; set; }
        public string MessageText { get; set; }
        public bool CanClose { get; set; }
        public MessageOption Option1 { get; set; }
        public MessageOption Option2 { get; set; }
    }
    
    [Serializable]
    public class MessageOption
    {
        public string Label { get; set; }
        public string value { get; set; }
    }
}