using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Beatmap
    {
        public string Name { get; set; }
        public string LevelId { get; set; }
        public Characteristic Characteristic { get; set; }
        public SharedConstructs.BeatmapDifficulty Difficulty { get; set; }
    }
}
