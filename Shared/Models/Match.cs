using System;
using static TournamentAssistantShared.SharedConstructs;

namespace TournamentAssistantShared.Models
{
    [Serializable]

    public class Match
    {
        public string Guid { get; set; }
        public Player[] Players { get; set; }
        public MatchCoordinator Coordinator { get; set; }

        //The following are created and modified by the match coordinator
        public PreviewBeatmapLevel CurrentlySelectedMap { get; set; }
        public Characteristic CurrentlySelectedCharacteristic { get; set; }
        public BeatmapDifficulty CurrentlySelectedDifficulty { get; set; }
    }
}
