using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    class Event
    {
        public enum EventType
        {
            PlayerAdded,
            PlayerUpdated,
            PlayerLeft,
            CoordinatorAdded,
            CoordinatorLeft,
            MatchCreated,
            MatchUpdated,
            MatchDeleted
        }

        public EventType Type { get; set; }
        public object ChangedObject { get; set; }
    }
}
