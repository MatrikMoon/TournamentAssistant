using System;
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

        //Moon's note: This is just a shortcut so the bindings can more easily access info like njs and notes for the currently selected difficulty
        public Diff CurrentDiff => SongInfo.CurrentVersion.diffs.FirstOrDefault(x => 
            Enum.TryParse<BeatmapDifficulty>(x.difficulty, ignoreCase: true, out var difficulty) && difficulty == SelectedDifficulty
        );

        public BeatmapDifficulty SelectedDifficulty { get; set; }
        public Characteristic SelectedCharacteristic { get; set; }
        public Characteristic[] Characteristics => SongInfo.Characteristics.Select(x => new Characteristic()
        {
            SerializedName = x,
            Difficulties = SongInfo.GetDifficultiesAsIntArray(x).Select(y => (BeatmapDifficulty)y).ToArray()
        }).ToArray();
    }
}
