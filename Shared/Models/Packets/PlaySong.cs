using System;

namespace TournamentAssistantShared.Models.Packets
{
    [Serializable]
    public class PlaySong
    {
        public string levelId;
        public Characteristic characteristic;
        public SharedConstructs.BeatmapDifficulty difficulty;
        public PlayerSpecificSettings playerSettings;
        public GameplayModifiers gameplayModifiers;
        public bool playWithStreamSync;
    }
}
