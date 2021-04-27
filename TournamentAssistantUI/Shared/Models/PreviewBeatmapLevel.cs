using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class PreviewBeatmapLevel
    {
        // -- Unloaded levels have the following:
        public string LevelId { get; set; }
        public string Name { get; set; }

        // -- Only Loaded levels will have the following:
        public Characteristic[] Characteristics { get; set; }

        public bool Loaded { get; set; } = false;
    }
}
