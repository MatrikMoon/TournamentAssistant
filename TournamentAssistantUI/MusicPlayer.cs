using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Media;
using LibVLCSharp.Shared;
using TournamentAssistantShared;
using System.Threading;

namespace TournamentAssistantUI
{
    class MusicPlayer
    {
        public LibVLC VLC;
        public MediaPlayer player;
        public MusicPlayer()
        {
            Core.Initialize();
            VLC = new LibVLC();
            player = new MediaPlayer(VLC)
            {
                EnableHardwareDecoding = true
            };
            player.EndReached += Player_EndReached;
        }

        //LibVLC bug workaround
        private void Player_EndReached(object sender, EventArgs e)
        {
            Task.Run(() => player.Stop());
        }

        /// <summary>
        /// Initializes a media file
        /// </summary>
        /// <param name="path">File path to initialize</param>
        /// <returns>Media representation of the file at the provided path</returns>
        public Media MediaInit(string path)
        {
            var media = new Media(VLC, path);
            media.Parse();
            return media;
        }
    }
}
