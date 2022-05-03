using System.Collections.Generic;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 2/13/2022
 * Allows for Unioning CoreServers. This could be abstracted out to handle all proto types in conjunction with
 * ProtobufExtensions.cs, but I'll leave it specific for now
 */

namespace TournamentAssistantShared.Utilities
{
    public class CoreServerEqualityComparer : IEqualityComparer<CoreServer>
    {
        public bool Equals(CoreServer x, CoreServer y)
        {
            return x.CoreServerEquals(y);
        }

        public int GetHashCode(CoreServer obj)
        {
            return obj.Address.GetHashCode()
                + obj.Name.GetHashCode()
                + obj.Port.GetHashCode();
        }
    }
}
