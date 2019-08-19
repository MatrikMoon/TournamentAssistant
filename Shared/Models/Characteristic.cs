using System;
using System.Collections.Generic;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Characteristic
    {
        public string SerializedName { get; set; }

        public BeatmapDifficulty[] difficulties { get; set; }
    }
}
