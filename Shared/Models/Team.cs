using System;
using System.Collections.Generic;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Team
    {
        public string Guid { get; set; }
        public string Name { get; set; }

        #region Equality
        public static bool operator ==(Team a, Team b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return ReferenceEquals(a, null) && ReferenceEquals(b, null);
            return a.GetHashCode() == b.GetHashCode();
        }

        public static bool operator !=(Team a, Team b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return ReferenceEquals(a, null) ^ ReferenceEquals(b, null);
            return a.GetHashCode() != b.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (!(other is User)) return false;
            return Guid == (other as User).Guid;
        }

        public override int GetHashCode()
        {
            var hashCode = -1292076098;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Guid);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            return hashCode;
        }
        #endregion Equality
    }
}
