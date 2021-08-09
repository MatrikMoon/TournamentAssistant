using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class LoadSong
    {
        public string LevelId { get; set; }
        public string CustomHostUrl { get; set; }
    }
}
