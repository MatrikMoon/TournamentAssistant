using System;

namespace BattleSaberShared.Models.Packets
{
    [Serializable]
    public class SongList
    {
        public PreviewBeatmapLevel[] Levels { get; set; }
    }
}
