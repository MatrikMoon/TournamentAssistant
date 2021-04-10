using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class SongList
    {
        public PreviewBeatmapLevel[] Levels { get; set; }
    }
}
