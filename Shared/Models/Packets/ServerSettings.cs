using System;

namespace BattleSaberShared.Models.Packets
{
    [Serializable]
    public class ServerSettings
    {
        public bool teams;
        public bool tournamentMode;
    }
}