using System;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Player : User
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

        public ulong UserId { get; set; }
        public Team Team { get; set; }
        public PlayState CurrentPlayState { get; set; }
        public DownloadState CurrentDownloadState { get; set; }
        public int CurrentScore { get; set; }
        public SongList SongList { get; set; }

        //Stream sync
        public Point StreamScreenCoordinates;
        public long StreamDelayMs { get; set; }
        public long StreamSyncStartMs { get; set; }
    }
}
