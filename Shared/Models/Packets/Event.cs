using System;

namespace BattleSaberShared.Models.Packets
{
    [Serializable]
    class Event
    {
        public enum EventType
        {
            PlayerAdded,
            PlayerUpdated,
            PlayerLeft,
            PlayerFinishedSong,
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
