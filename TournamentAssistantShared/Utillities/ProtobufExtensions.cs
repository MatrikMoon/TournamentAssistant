using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 9/9/2021
 * Extension methods for working with these proto packets
 * Particularly, this helper came around when the need arose for custom equality between proto packets
 */

namespace TournamentAssistantShared.Utillities
{
    public static class ProtobufExtensions
    {
        public static bool UserEquals(this User firstUser, User secondUser)
        {
            return firstUser.Id == secondUser.Id;
        }

        public static bool ContainsUser(this ICollection<User> users, User user)
        {
            return users.Any(x => x.UserEquals(user));
        }

        public static bool PlayerEquals(this Player firstPlayer, Player secondPlayer)
        {
            return firstPlayer.User.UserEquals(secondPlayer.User);
        }

        public static bool ContainsPlayer(this ICollection<Player> players, Player player)
        {
            return players.Any(x => x.PlayerEquals(player));
        }

        public static bool MatchEquals(this Match firstMatch, Match secondMatch)
        {
            return firstMatch.Guid == secondMatch.Guid;
        }

        public static bool ContainsMatch(this ICollection<Match> matches, Match match)
        {
            return matches.Any(x => x.MatchEquals(match));
        }

        public static bool CoreServerEquals(this CoreServer firstServer, CoreServer secondServer)
        {
            return firstServer.Address == secondServer.Address &&
                firstServer.Port == secondServer.Port;
        }

        public static bool ContainsCoreServer(this ICollection<CoreServer> coreServers, CoreServer coreServer)
        {
            return coreServers.Any(x => x.CoreServerEquals(coreServer));
        }

        public static byte[] ProtoSerialize<T>(this T record) where T : class
        {
            if (null == record) return null;

            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, record);
                return stream.ToArray();
            }
        }

        public static T ProtoDeserialize<T>(this byte[] data) where T : class
        {
            if (null == data) return null;

            using (var stream = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(stream);
            }
        }
    }
}
