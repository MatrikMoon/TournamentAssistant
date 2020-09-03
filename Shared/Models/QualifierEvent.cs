using System;
using TournamentAssistantShared.Models.Discord;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class QualifierEvent
    {
        public string Name { get; set; }
        public Guild Guild { get; set; }
        public Channel InfoChannel { get; set; }
        public PreviewBeatmapLevel[] QualifierMaps { get; set; }
        public bool ShowScores { get; set; }
    }
}