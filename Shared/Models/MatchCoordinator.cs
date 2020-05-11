using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class MatchCoordinator : User
    {
        // -- The chips on the match coordinator view require this for the purpose of the little chip icon
        public string GetIcon
        {
            get
            {
                return !string.IsNullOrEmpty(Name) ? Name.Substring(0, 1) : "X";
            }
        }
    }
}
