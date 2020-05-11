using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class ConnectResponse
    {
        public enum ResponseType
        {
            Success,
            Fail
        }

        public ResponseType type;
        public User self; //The newly connected client needs to know what guid we've assigned it, etc
        public string message; //We can display a message in case of failure
        public int serverVersion; //Just in case there's version mix-n-matching someday
    }
}
