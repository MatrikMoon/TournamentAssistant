using System;

namespace BattleSaberShared.Models
{
    [Serializable]
    public class PlayerSpecificSettings
    {
        [Flags]
        public enum PlayerOptions
        {
            None = 0,
            LeftHanded = 1,
            StaticLights = 2,
            NoHud = 4,
            AdvancedHud = 8,
            ReduceDebris = 16
        }

        public PlayerOptions Options { get; set; }
    }
}
