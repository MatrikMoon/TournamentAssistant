using System.Collections.Generic;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 2/13/2022
 * Allows for Unioning CoreServers. This could be abstracted out to handle all proto types in conjunction with
 * ProtobufExtensions.cs, but I'll leave it specific for now
 */

namespace TournamentAssistantShared.Utilities
{
    public class PlayerEqualityComparer : IEqualityComparer<Player>
    {
        public bool Equals(Player x, Player y)
        {
            return x.PlayerEquals(y);
        }

        public int GetHashCode(Player obj)
        {
            return obj.User.Id.GetHashCode()
                + obj.User.Name.GetHashCode();
        }
    }
}
