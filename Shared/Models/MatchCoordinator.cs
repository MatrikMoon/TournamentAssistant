using System;

namespace BattleSaberShared.Models
{
    [Serializable]
    public class MatchCoordinator : User
    {
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
