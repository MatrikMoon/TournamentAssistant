using System;
using System.Collections.Generic;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class CoreServer
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }

        #region Equality
        public static bool operator ==(CoreServer a, CoreServer b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return ReferenceEquals(a, null) && ReferenceEquals(b, null);
            return a.GetHashCode() == b.GetHashCode();
        }

        public static bool operator !=(CoreServer a, CoreServer b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return ReferenceEquals(a, null) ^ ReferenceEquals(b, null);
            return a.GetHashCode() != b.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (!(other is CoreServer)) return false;
            return Address == (other as CoreServer).Address && Port == (other as CoreServer).Port;
        }

        public override int GetHashCode()
        {
            var hashCode = -1292076098;
            hashCode = hashCode * -1521134295 + EqualityComparer<int>.Default.GetHashCode(Port);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Address);
            return hashCode;
        }
        #endregion Equality
    }
}
