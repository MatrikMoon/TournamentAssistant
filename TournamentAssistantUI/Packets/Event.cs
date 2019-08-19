using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TournamentAssistantUI.Packets
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
            MatchDeleted,
            SetSelf
        }

        public EventType eventType;
        public object changedObject;
    }
}
