using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Event
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
            MatchDeleted,
            QualifierEventCreated,
            QualifierEventUpdated,
            QualifierEventDeleted,
            HostAdded,
            HostRemoved
        }

        public EventType Type { get; set; }
        public object ChangedObject { get; set; }
    }
}
