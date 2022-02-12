﻿using ProtoBuf;
using System.IO;
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

        public static bool MatchEquals(this Match firstMatch, Match secondMatch)
        {
            return firstMatch.Guid == secondMatch.Guid;
        }

        public static bool CoreServerEquals(this CoreServer firstServer, CoreServer secondServer)
        {
            return firstServer.Address == secondServer.Address &&
                firstServer.Port == secondServer.Port;
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