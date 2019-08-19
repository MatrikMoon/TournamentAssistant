using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class MatchCoordinator
    {
        public string Guid { get; set; }
        public string Name { get; set; }

        // -- The chips on the match coordinator view require this for the purpose of the little chip icon
        public string GetIcon
        {
            get
            {
                return Name.Substring(0, 1);
            }
        }
    }
}
