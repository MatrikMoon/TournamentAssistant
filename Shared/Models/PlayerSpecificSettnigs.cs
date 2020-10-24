using System;

namespace TournamentAssistantShared.Models
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
            ReduceDebris = 16,
            AutoPlayerHeight = 32,
            NoFailEffects = 64,
            AutoRestart = 128,
            HideNoteSpawnEffect = 256,
            AdaptiveSfx = 512
        }

        public float PlayerHeight { get; set; } = 1.625f;
        public float SfxVolume { get; set; } = 0.7f;
        public float SaberTrailIntensity { get; set; } = 0.4f;
        public float NoteJumpStartBeatOffset { get; set; } = 0f;

        public PlayerOptions Options { get; set; }
    }
}
