using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class LoadedSong
    {
        public PreviewBeatmapLevel Level { get; set; }
    }
}
