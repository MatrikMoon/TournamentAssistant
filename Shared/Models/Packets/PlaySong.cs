using System;

namespace BattleSaberShared.Models.Packets
{
    [Serializable]
    public class PlaySong
    {
        public Beatmap beatmap;

        public PlayerSpecificSettings playerSettings;
        public GameplayModifiers gameplayModifiers;

        public bool streamSync;
        public bool floatingScoreboard;
    }
}
