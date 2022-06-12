using System.Collections.Generic;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 2/13/2022
 * Allows for Unioning CoreServers. This could be abstracted out to handle all proto types in conjunction with
 * ProtobufExtensions.cs, but I'll leave it specific for now
 */

namespace TournamentAssistantShared.Utilities
{
    public class UserEqualityComparer : IEqualityComparer<User>
    {
        public bool Equals(User x, User y)
        {
            return x.UserEquals(y);
        }

        public int GetHashCode(User obj)
        {
            return obj.Guid.GetHashCode()
                + obj.Name.GetHashCode();
        }
    }
}
