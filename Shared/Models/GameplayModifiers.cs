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
            NoArrows = 2^2,
            NoObstacles = 2^3,
            SlowSong = 2^4,

            //Positive Modifiers
            InstaFail = 2^5,
            FailOnClash = 2^6,
            BatteryEnergy = 2^7,
            FastNotes = 2^8,
            FastSong = 2^9,
            DisappearingArrows = 2^10,
            GhostNotes = 2^11,

            //1.12.2 Additions
            DemoNoFail = 2^12,
            DemoNoObstacles = 2^13,
            StrictAngles = 2^14,

            //1.13.4 Additions
            ProMode = 2^15,
            ZenMode = 2^16,
            SmallCubes = 2^17,
            SuperFastSong = 2^18
        }

        public GameOptions Options { get; set; }
    }
}
