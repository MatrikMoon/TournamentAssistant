using System;
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

        public PlayState CurrentPlayState { get; set; }
        public DownloadState CurrentDownloadState { get; set; }

        public string Guid { get; set; }
        public string Name { get; set; }
        public SongList SongList { get; set; }
    }
}
