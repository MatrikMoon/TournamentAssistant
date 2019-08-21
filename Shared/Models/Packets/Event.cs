using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    class Event
    {
        public enum EventType
        {
            PlayerAdded,
            PlayerLeft,
            CoordinatorAdded,
            CoordinatorLeft,
            MatchCreated,
            MatchUpdated,
            MatchDeleted,
            SetSelf
        }

        public EventType eventType;
        public object changedObject;
    }
}
