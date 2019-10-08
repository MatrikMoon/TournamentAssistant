using System;
using System.Collections.Generic;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Player
    {
        public enum PlayState
        {
            Waiting,
            InGame,
        }

        public enum DownloadState
        {
            None,
            Downloading,
            Downloaded,
            DownloadError
        }

        [Serializable]
        public struct Point
        {
            public int x;
            public int y;
        }

        public PlayState CurrentPlayState { get; set; }
        public DownloadState CurrentDownloadState { get; set; }
        public int CurrentScore { get; set; }

        public string Guid { get; set; }
        public string Name { get; set; }
        public SongList SongList { get; set; }

        //Stream sync
        public Point StreamScreenCoordinates;

        #region Equality
        public static bool operator ==(Player a, Player b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
            return a.GetHashCode() == b.GetHashCode();
        }

        public static bool operator !=(Player a, Player b)
        {
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
            return a.GetHashCode() != b.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (!(other is Player)) return false;
            return Guid == (other as Player).Guid;
        }

        public override int GetHashCode()
        {
            var hashCode = -1292076098;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Guid);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            return hashCode;
        }
        #endregion Equality
    }
}
