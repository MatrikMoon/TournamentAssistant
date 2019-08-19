using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class SongList
    {
        public PreviewBeatmapLevel[] Levels { get; set; }
    }
}
