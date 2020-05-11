using System;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class GameplayModifiers
    {
        [Flags]
        public enum GameOptions
        {
            None = 0,

            //Negative modifiers
            NoFail = 1,
            NoBombs = 2,
            NoObstacles = 4,
            SlowSong = 8,

            //Positive Modifiers
            InstaFail = 32,
            FailOnClash = 64,
            BatteryEnergy = 128,
            FastNotes = 256,
            FastSong = 512,
            DisappearingArrows = 1024,
            GhostNotes = 2048
        }

        public GameOptions Options { get; set; }
    }
}
