using System.Linq;
using TournamentAssistantShared.Models;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.BeatSaver
{
    public class PlaylistItem
    {
        public SongInfo SongInfo { get; set; }
        public bool Played { get; set; }
        public string SongDataPath { get; set; }
        public string CoverPath { get; set; }
        public string DurationString { get; set; }

        public BeatmapDifficulty SelectedDifficulty { get; set; }
        public Characteristic SelectedCharacteristic { get; set; }
        public Characteristic[] Characteristics => SongInfo.Characteristics.Select(x => new Characteristic()
        {
            SerializedName = x,
            Difficulties = SongInfo.GetDifficultiesAsIntArray(x).Select(y => (BeatmapDifficulty)y).ToArray()
        }).ToArray();
    }
}
