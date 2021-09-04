using System;
using System.Collections.Generic;
using System.Text;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class RoomRule
    {
        public int AmountOfPlayers { get; set; }
        public int PlayersToKick { get; set; }
    }
}
