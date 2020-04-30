using System;

namespace BattleSaberShared.Models
{
    [Serializable]
    public class Characteristic
    {
        public string SerializedName { get; set; }

        public SharedConstructs.BeatmapDifficulty[] Difficulties { get; set; }

        public override string ToString() => SerializedName;
    }
}
