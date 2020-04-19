using System;
using System.Collections.Generic;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class User
    {
        public string Guid { get; set; }
        public string Name { get; set; }

        #region Equality
        public static bool operator ==(User a, User b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
            return a.GetHashCode() == b.GetHashCode();
        }

        public static bool operator !=(User a, User b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
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
