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
            NoArrows = 4,
            NoObstacles = 8,
            SlowSong = 16,

            //Positive Modifiers
            InstaFail = 32,
            FailOnClash = 64,
            BatteryEnergy = 128,
            FastNotes = 256,
            FastSong = 512,
            DisappearingArrows = 1024,
            GhostNotes = 2048,

            //1.12.2 Additions
            DemoNoFail = 4096,
            DemoNoObstacles = 8192,
            StrictAngles = 16384,

            //1.13.4 Additions
            ProMode = 32768,
            ZenMode = 65536,
            SmallCubes = 131072,
            SuperFastSong = 262144
        }

        public GameOptions Options { get; set; }
    }
}
