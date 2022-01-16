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

        public static bool CoreServerEquals(this CoreServer firstServer, CoreServer secondServer)
        {
            return firstServer.Address == secondServer.Address &&
                firstServer.Port == secondServer.Port;
        }
    }
}
