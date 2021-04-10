using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class Response
    {
        public enum ResponseType
        {
            Success,
            Fail
        }

        public ResponseType Type { get; set; }
        public string Message { get; set; } //We can display a message in case of failure
    }
}
