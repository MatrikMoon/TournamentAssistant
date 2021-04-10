using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class PlaySong
    {
        public GameplayParameters GameplayParameters { get; set; }

        public bool FloatingScoreboard { get; set; }
        public bool StreamSync { get; set; }
        public bool DisableFail { get; set; }
        public bool DisablePause { get; set; }
        public bool DisableScoresaberSubmission { get; set; }
        public bool ShowNormalNotesOnStream { get; set; }
    }
}
