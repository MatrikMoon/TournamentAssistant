using System;
using TournamentAssistantShared.Models.Packets;
using static TournamentAssistantShared.GlobalConstants;

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class Player : User
    {
        public enum PlayStates
        {
            Waiting,
            InGame,
        }

        public enum DownloadStates
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

        public string UserId { get; set; }
        public string AvatarURL => $"{ScoreSaberAPI}static/avatars/{UserId}.jpg";
        public Team Team { get; set; }
        public PlayStates PlayState { get; set; }
        public DownloadStates DownloadState { get; set; }
        public int Score { get; set; }
        public int Combo { get; set; }
        public float Accuracy { get; set; }
        public float SongPosition { get; set; }
        public SongList SongList { get; set; }
        public string[] ModList { get; set; }

        //Stream sync
        public Point StreamScreenCoordinates { get; set; }
        public long StreamDelayMs { get; set; }
        public long StreamSyncStartMs { get; set; }
    }
}
