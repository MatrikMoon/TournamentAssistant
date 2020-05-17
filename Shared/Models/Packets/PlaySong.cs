using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class PlaySong
    {
        public Beatmap Beatmap { get; set; }

        public PlayerSpecificSettings PlayerSettings { get; set; }
        public GameplayModifiers GameplayModifiers { get; set; }

        public bool StreamSync { get; set; }
        public bool FloatingScoreboard { get; set; }
    }
}
