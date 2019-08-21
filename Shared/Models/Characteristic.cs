using System;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Characteristic
    {
        public string SerializedName { get; set; }

        public BeatmapDifficulty[] Difficulties { get; set; }

        public override string ToString() => SerializedName;
    }
}
