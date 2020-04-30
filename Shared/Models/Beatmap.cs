using System;

namespace BattleSaberShared.Models
{
    [Serializable]
    public class Beatmap
    {
        public string levelId;
        public Characteristic characteristic;
        public SharedConstructs.BeatmapDifficulty difficulty;
    }
}
