using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 9/9/2021
 * Extension methods for working with these proto packets
 * Particularly, this helper came around when the need arose for custom equality between proto packets
 */

namespace TournamentAssistantShared.Utilities
{
    public static class ProtobufExtensions
    {
        public static bool UserEquals(this User firstUser, User secondUser)
        {
            if ((firstUser == null) ^ (secondUser == null)) return false;
            else if ((firstUser == null) && (secondUser == null)) return true;
            return firstUser.Guid == secondUser.Guid;
        }

        public static bool ContainsUser(this IEnumerable<User> users, User user)
        {
            // .toList() ensures we don't have to worry about the enumerable being
            // modified while we're looking at it
            return users.ToList().Any(x => x.UserEquals(user));
        }

        public static bool MatchEquals(this Match firstMatch, Match secondMatch)
        {
            if ((firstMatch == null) ^ (secondMatch == null)) return false;
            else if ((firstMatch == null) && (secondMatch == null)) return true;
            return firstMatch.Guid == secondMatch.Guid;
        }

        public static bool ContainsMatch(this IEnumerable<Match> matches, Match match)
        {
            // .toList() ensures we don't have to worry about the enumerable being
            // modified while we're looking at it
            return matches.ToList().Any(x => x.MatchEquals(match));
        }

        public static bool CoreServerEquals(this CoreServer firstServer, CoreServer secondServer)
        {
            if ((firstServer == null) ^ (secondServer == null)) return false;
            else if ((firstServer == null) && (secondServer == null)) return true;
            return firstServer.Address == secondServer.Address &&
                firstServer.Port == secondServer.Port;
        }

        public static bool ContainsCoreServer(this IEnumerable<CoreServer> coreServers, CoreServer coreServer)
        {
            // .toList() ensures we don't have to worry about the enumerable being
            // modified while we're looking at it
            return coreServers.ToList().Any(x => x.CoreServerEquals(coreServer));
        }

        public static byte[] ProtoSerialize<T>(this T record) where T : class
        {
            if (null == record) return null;

            using (var stream = new MemoryStream())
            {
                try
                {
                    Serializer.Serialize(stream, record);
                    return stream.ToArray();
                }
                catch (InvalidOperationException e)
                {
                    Logger.Success(Environment.StackTrace);
                    Logger.Error(e);
                    Logger.Error(e.Message);
                    throw e;
                }
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
