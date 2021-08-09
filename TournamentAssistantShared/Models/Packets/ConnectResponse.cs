using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class ConnectResponse : Response
    {
        public User Self { get; set; } //The newly connected client needs to know what guid we've assigned it, etc
        public State State { get; set; } //The current network state for the new clietn
        public int ServerVersion { get; set; } //Just in case there's version mix-n-matching someday
    }
}
