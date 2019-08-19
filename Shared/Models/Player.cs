using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Player
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public SongList SongList { get; set; }
    }
}
